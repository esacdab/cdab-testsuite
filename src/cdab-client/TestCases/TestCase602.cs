/*
cdab-client is part of the software suite used to run Test Scenarios 
for bechmarking various Copernicus Data Provider targets.
    
    Copyright (C) 2020 Terradue Ltd, www.terradue.com
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published
    by the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using cdabtesttools.Config;
using cdabtesttools.Data;
using cdabtesttools.Measurement;
using cdabtesttools.Target;
using log4net;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Result;
using Terradue.Metadata.EarthObservation.OpenSearch.Extensions;

namespace cdabtesttools.TestCases
{
    internal class TestCase602 : TestCase201
    {
        // S2B_MSIL1C_20220124T051219_N0301_R033_T41FQE_20220124T063342
        // S2B_OPER_MSI_L1C_TL_VGS2_20220124T063342_A025513_T41FQE_N03.01
        private static Regex sentinel2UidRegex = new Regex(@"S2[AB]_MSI..._.{15}_N.{4}_R.{3}_(?'tile'T.{5})_.{15}");
        private static Regex sentinel2TileRegex = new Regex(@"S2[AB]_OPER_MSI_..._.._...._.{15}_A.{6}_(?'tile'T.{5})_N.{2}\...");
        private Dictionary<int, CrossCatalogueCoverageFiltersDefinition> queryFiltersTuple;

        public override bool MarkUnsupportedData => true;

        public TestCase602(ILog log, TargetSiteWrapper target, int load_factor, out List<IOpenSearchResultItem> foundItems) :
            base(log, target, load_factor, null, out foundItems)
        {
            Id = "TC602";
            Title = "Data Availability Latency Analysis";
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);
            queryFilters = new ConcurrentQueue<FiltersDefinition>();
            queryFiltersTuple = new Dictionary<int, CrossCatalogueCoverageFiltersDefinition>();
            int i = 0;
            foreach (var coverage in target.TargetSiteConfig.Data.Catalogue.Sets.Where(cs => cs.Value.Type == TargetCatalogueSetType.local))
            {
                foreach (var division in DataHelper.GenerateDataAvailabilityLatencyFiltersDefinition(coverage.Key, coverage.Value, target))
                {
                    queryFiltersTuple.Add(i, division);
                    division.Target.FiltersDefinition.AddFilter("queryId", "dummy", i.ToString(), "", null, null);
                    queryFilters.Enqueue(division.Target.FiltersDefinition);
                    i++;
                }
            }
        }

        public override TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks)
        {

            try
            {
                results = tasks.Result.ToList();
                var testCaseResults = MeasurementsAnalyzer.GenerateTestCase602Result(this);
                testCaseResults.SearchFiltersDefinition = results.Select(tcr => tcr.FiltersDefinition).ToList();
                return testCaseResults;
            }
            catch (AggregateException e)
            {
                log.WarnFormat("Test Case Execution Error : {0}", e.InnerException.Message);
                throw e;
            }
        }


        public override IEnumerable<TestUnitResult> RunTestUnits(TaskFactory taskFactory, Task prepTask)
        {
            if (StartTime.Ticks == 0)
                StartTime = DateTimeOffset.UtcNow;

            if (prepTask.IsFaulted)
                throw prepTask.Exception;

            List<Task<TestUnitResult>> _testUnits = new List<Task<TestUnitResult>>();
            Task[] previousTask = Array.ConvertAll(new int[target.TargetSiteConfig.MaxCatalogueThread], mp => prepTask);

            int i = queryFilters.Count();

            while (i > 0)
            {
                for (int j = 0; j < target.TargetSiteConfig.MaxCatalogueThread; j++)
                {
                    var _testUnit = previousTask[j].ContinueWith<KeyValuePair<IOpenSearchable, FiltersDefinition>>((task) =>
                    {
                        prepTask.Wait();
                        FiltersDefinition randomFilter;
                        queryFilters.TryDequeue(out randomFilter);
                        return new KeyValuePair<IOpenSearchable, FiltersDefinition>(target.CreateOpenSearchableEntity(randomFilter, Configuration.Current.Global.QueryTryNumber), randomFilter);
                    }).
                        ContinueWith((request) =>
                        {
                            if (request.Result.Value == null) return null;

                            // Get the original collection from the filter definition and check whether polling is needed
                            FiltersDefinition parameters = request.Result.Value;
                            bool latencyPolling = target.TargetSiteConfig.Data.Catalogue.LatencyPolling != null && target.TargetSiteConfig.Data.Catalogue.LatencyPolling.Value;
                            if (parameters.DataCollection != null && parameters.DataCollection.LatencyPolling != null) latencyPolling = parameters.DataCollection.LatencyPolling.Value;

                            if (latencyPolling)
                            {
                                // For providers that do not offer online dates, use special processing
                                log.Info("Latencies calculated by polling the target catalogue; process may take some time");

                                int queryId = int.Parse(parameters.Filters.FirstOrDefault(f => f.Key == "queryId").Value);
                                IOpenSearchable targetEntity = request.Result.Key;
                                IOpenSearchable referenceEntity = queryFiltersTuple[queryId].Reference.Target.CreateOpenSearchableEntity();

                                return PollUntilAvailable(targetEntity, referenceEntity, parameters);
                            }
                            else
                            {
                                return MakeQuery(request.Result.Key, request.Result.Value);
                            }
                        });
                    _testUnits.Add(_testUnit);
                    previousTask[j] = _testUnit;
                    i--;
                }
            }
            try
            {
                Task.WaitAll(_testUnits.ToArray());
            }
            catch (AggregateException ae)
            {
                Exception ex = ae;
                while (ex is AggregateException)
                {
                    ex = ex.InnerException;
                }
                log.Debug(ex.Message);
                log.Debug(ex.StackTrace);
            }
            EndTime = DateTimeOffset.UtcNow;

            return _testUnits.Select(t => t.Result).Where(r => r != null);
        }


        public TestUnitResult PollUntilAvailable(IOpenSearchable targetEntity, IOpenSearchable referenceEntity, FiltersDefinition fd)
        {

            List<IMetric> metrics = new List<IMetric>();

            log.DebugFormat("[{1}] > Query {0} {2}...", fd.Name, Task.CurrentId, fd.Label);
 
            var parameters = fd.GetNameValueCollection();

            return catalogue_task_factory.StartNew(() =>
            {
                int latencyCheckOffset = (target.TargetSiteConfig.Data.Catalogue.LatencyCheckOffset != null ? target.TargetSiteConfig.Data.Catalogue.LatencyCheckOffset.Value : 0);
                if (fd.DataCollection != null && fd.DataCollection.LatencyCheckOffset != null) latencyCheckOffset = fd.DataCollection.LatencyCheckOffset.Value;

                if (latencyCheckOffset != 0)
                {
                    DateTime modifiedEnd = DateTime.UtcNow.AddSeconds(- latencyCheckOffset);
                    parameters["{http://purl.org/dc/terms/}modified"] = String.Format("2000-01-01T00:00:00Z/{0:yyyy-MM-ddTHH:mm:ssZ}", modifiedEnd);
                }
            
                foreach (var param in parameters.AllKeys)
                {
                    log.DebugFormat("- Parameter {0} = {1}", param, parameters[param]);
                }
                parameters["{http://a9.com/-/spec/opensearch/1.1/}count"] = "1";
                return ose.Query(referenceEntity, parameters);

            }).ContinueWith<TestUnitResult>(task =>
            {
                Terradue.OpenSearch.Result.IOpenSearchResultCollection results = null;
                try
                {
                    results = task.Result;
                }
                catch (AggregateException e)
                {
                    log.DebugFormat("[{0}] < No results for {2}. Exception: {1}", Task.CurrentId, e.InnerException.Message, fd.Label);
                    log.Debug(e.InnerException.StackTrace);
                    metrics.Add(new ExceptionMetric(e.InnerException));
                    LongMetric totalResultsMetric = metrics.FirstOrDefault(m => m.Name == MetricName.maxTotalResults) as LongMetric;
                    if (totalResultsMetric != null) metrics.Remove(totalResultsMetric);
                    metrics.Add(new LongMetric(MetricName.maxTotalResults, -1, "#"));
                    metrics.Add(new LongMetric(MetricName.totalReadResults, -1, "#"));
                }

                metrics.Add(new StringMetric(MetricName.dataCollectionDivision, fd.Label, "string"));
                if (results != null)
                {
                    foundItems.AddRange(results.Items);
                    foreach (var item in results.Items) {
                        log.DebugFormat("Item from reference catalogue: {0} ({1})", item.Identifier, item.GetType().Name);
                    }

                    Dictionary<IOpenSearchResultItem, bool> online = new Dictionary<IOpenSearchResultItem, bool>();
                    foreach (IOpenSearchResultItem item in results.Items) online[item] = false;

                    List<double> avaLatencies = new List<double>();
                    long validatedResults = 0;

                    int latencyCheckMaxDuration = (target.TargetSiteConfig.Data.Catalogue.LatencyCheckMaxDuration != null ? target.TargetSiteConfig.Data.Catalogue.LatencyCheckMaxDuration.Value : 3600);
                    if (fd.DataCollection != null && fd.DataCollection.LatencyCheckMaxDuration != null) latencyCheckMaxDuration = fd.DataCollection.LatencyCheckMaxDuration.Value;
                    int latencyCheckInterval = (target.TargetSiteConfig.Data.Catalogue.LatencyCheckInterval != null ? target.TargetSiteConfig.Data.Catalogue.LatencyCheckInterval.Value : 600);
                    if (fd.DataCollection != null && fd.DataCollection.LatencyCheckInterval != null) latencyCheckInterval = fd.DataCollection.LatencyCheckInterval.Value;

                    log.DebugFormat("Latency handling for {0}: max duration: {1} s, check ever {2} s", fd.Label, latencyCheckMaxDuration, latencyCheckInterval);

                    DateTime maxEndTime = DateTime.UtcNow.AddSeconds(latencyCheckMaxDuration);
                    log.InfoFormat("Checking for product availability in target until {0}", maxEndTime.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                    
                    bool oneMissing = true;
                    try {
                        int iterationCount = 0;

                        while (oneMissing && DateTime.UtcNow < maxEndTime) {
                            iterationCount += 1;

                            oneMissing = false;

                            foreach (IOpenSearchResultItem referenceItem in results.Items)
                            {
                                if (online[referenceItem]) continue;

                                Stopwatch sw = new Stopwatch();
                                DateTimeOffset timeStart = DateTimeOffset.UtcNow;
                                IOpenSearchResultItem targetItem = null;
                                IOpenSearchResultCollection targetResults = null;
                                sw.Start();
                                try
                                {
                                    targetItem = FindCorrespondingItem(
                                        referenceItem,
                                        targetEntity,
                                        fd,   // add original filters (product type may be needed in target)
                                        out targetResults
                                    );
                                }
                                catch (WebException we)
                                {
                                    log.WarnFormat("Error during target request for {0}", referenceItem.Id);
                                    log.WarnFormat("Error message: {0}", we.Message);
                                }
                                var respTime = sw.ElapsedMilliseconds;
                                sw.Stop();
                                DateTimeOffset timeStop = DateTimeOffset.UtcNow;

                                if (targetItem == null)
                                {
                                    log.InfoFormat("Item {0} not (yet) available in target", referenceItem.Identifier);
                                    oneMissing = true;
                                    continue;
                                }
                                online[referenceItem] = true;

                                long serializedSize = 0;
                                try
                                {
                                    serializedSize = Encoding.Default.GetBytes(targetResults.SerializeToString()).Length;
                                }
                                catch
                                {

                                }
                                var metricsArray = targetResults.ElementExtensions.ReadElementExtensions<Terradue.OpenSearch.Benchmarking.Metrics>("Metrics", "http://www.terradue.com/metrics", Terradue.OpenSearch.Benchmarking.MetricFactory.Serializer);
                                if (metricsArray == null || metricsArray.Count() == 0)
                                {
                                    log.Warn("No query metrics found! Response Time and error rate may be biased!");
                                    metrics.Add(new LongMetric(MetricName.responseTime, respTime, "ms"));
                                    metrics.Add(new LongMetric(MetricName.size, serializedSize, "bytes"));
                                    metrics.Add(new LongMetric(MetricName.beginGetResponseTime, timeStart.Ticks, "ticks"));
                                    metrics.Add(new LongMetric(MetricName.endGetResponseTime, timeStop.Ticks, "ticks"));
                                }
                                else
                                {
                                    Terradue.OpenSearch.Benchmarking.Metrics osMetrics = metricsArray.First();
                                    Terradue.OpenSearch.Benchmarking.Metric _sizeMetric = osMetrics.Metric.FirstOrDefault(m => m.Identifier == "size");
                                    Terradue.OpenSearch.Benchmarking.Metric _responseTimeMetric = osMetrics.Metric.FirstOrDefault(m => m.Identifier == "responseTime");
                                    Terradue.OpenSearch.Benchmarking.Metric _retryNumberMetric = osMetrics.Metric.FirstOrDefault(m => m.Identifier == "retryNumber");
                                    Terradue.OpenSearch.Benchmarking.Metric _beginGetResponseTime = osMetrics.Metric.FirstOrDefault(m => m.Identifier == "beginGetResponseTime");
                                    Terradue.OpenSearch.Benchmarking.Metric _endGetResponseTime = osMetrics.Metric.FirstOrDefault(m => m.Identifier == "endGetResponseTime");

                                    log.DebugFormat("[{4}] < {0}/{1} entries for {6} {5}. {2}bytes in {3}ms", targetResults.Count, targetResults.TotalResults, _sizeMetric.Value, _responseTimeMetric.Value, Task.CurrentId, fd.Label, fd.Name);
                                    if (_responseTimeMetric != null)
                                    {
                                        metrics.Add(new LongMetric(MetricName.responseTime, Convert.ToInt64(_responseTimeMetric.Value), "ms"));
                                    }
                                    else
                                    {
                                        metrics.Add(new LongMetric(MetricName.responseTime, respTime, "ms"));
                                    }

                                    if (_sizeMetric != null)
                                        metrics.Add(new LongMetric(MetricName.size, Convert.ToInt64(_sizeMetric.Value), "bytes"));
                                    else
                                        metrics.Add(new LongMetric(MetricName.size, serializedSize, "bytes"));

                                    if (_retryNumberMetric != null)
                                        metrics.Add(new LongMetric(MetricName.retryNumber, Convert.ToInt64(_retryNumberMetric.Value), "#"));
                                    else
                                        metrics.Add(new LongMetric(MetricName.retryNumber, 1, "#"));

                                    if (_beginGetResponseTime != null && _endGetResponseTime != null)
                                    {
                                        metrics.Add(new LongMetric(MetricName.beginGetResponseTime, Convert.ToInt64(_beginGetResponseTime.Value), "ticks"));
                                        metrics.Add(new LongMetric(MetricName.endGetResponseTime, Convert.ToInt64(_endGetResponseTime.Value), "ticks"));
                                    }
                                    else
                                    {
                                        metrics.Add(new LongMetric(MetricName.beginGetResponseTime, timeStart.Ticks, "ticks"));
                                        metrics.Add(new LongMetric(MetricName.endGetResponseTime, timeStop.Ticks, "ticks"));
                                    }
                                }

                                DateTimeOffset creationDate = DateTimeOffset.UtcNow;
                                log.DebugFormat("Target item estimated creation date ({0}): {1}", referenceItem.Id, creationDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                                DateTimeOffset ingestionDate = DateTime.MinValue;
                                var dateStrings = referenceItem.ElementExtensions.ReadElementExtensions<string>("ingestionDate", "http://www.terradue.com/");
                                if (dateStrings != null && dateStrings.Count() > 0)
                                {
                                    DateTimeOffset.TryParse(dateStrings.FirstOrDefault(), System.Globalization.CultureInfo.InstalledUICulture, System.Globalization.DateTimeStyles.AssumeUniversal, out ingestionDate);
                                }
                                log.DebugFormat("Reference item ingestion date ({0}): {1}", referenceItem.Identifier, ingestionDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                                
                                double latencySeconds = creationDate.Subtract(ingestionDate).TotalSeconds;
                                log.DebugFormat("Latency (seconds) ({0}): {1}", referenceItem.Id, latencySeconds);

                                avaLatencies.Add(latencySeconds);
                                validatedResults++;
                            }

                            if (oneMissing) {
                                log.InfoFormat("Sleep for {0} seconds", latencyCheckInterval);
                                Thread.Sleep(latencyCheckInterval * 1000);
                            }

                        }

                        if (oneMissing)
                        {
                            log.WarnFormat("Target item not online within time limit");
                        }

                        LongMetric totalResultsMetric = metrics.FirstOrDefault(m => m.Name == MetricName.maxTotalResults) as LongMetric;
                        if (totalResultsMetric != null) metrics.Remove(totalResultsMetric);
                        metrics.Add(new LongMetric(MetricName.maxTotalResults, 1, "#"));
                        metrics.Add(new LongMetric(MetricName.wrongResultsCount, results.Items.Count() - validatedResults, "#"));
                        metrics.Add(new LongMetric(MetricName.totalValidatedResults, validatedResults, "#"));
                        metrics.Add(new LongMetric(MetricName.totalReadResults, results.Items.Count(), "#"));
                        
                        if (avaLatencies.Count() > 0)
                        {
                            metrics.Add(new LongMetric(MetricName.avgDataAvailabilityLatency, (long)avaLatencies.Average(), "sec"));
                            metrics.Add(new LongMetric(MetricName.maxDataAvailabilityLatency, (long)avaLatencies.Max(), "sec"));
                        }
                        else
                        {
                            metrics.Add(new LongMetric(MetricName.avgDataOperationalLatency, -1, "sec"));
                            metrics.Add(new LongMetric(MetricName.maxDataOperationalLatency, -1, "sec"));
                        }
                        //metrics.Add(new LongMetric(MetricName.analysisTime, sw3.ElapsedMilliseconds, "msec"));
                    }
                    catch (Exception e)
                    {
                        metrics.Add(new ExceptionMetric(e));
                        log.ErrorFormat("[{0}] < Analysis failed for results {2} : {1}", Task.CurrentId, e.Message, fd.Label);
                        log.Debug(e.StackTrace);
                    }

                }

                return new TestUnitResult(metrics, fd);
            }).Result;
        }        


        protected override IEnumerable<IMetric> AnalyzeResults(IOpenSearchResultCollection results, FiltersDefinition fd)
        {
            List<IMetric> metrics = new List<IMetric>();
            long validatedResults = 0;
            long wrongResults = 0;

            log.DebugFormat("[{1}] Validating and Analyzing {0} result items...", results.Items.Count(), Task.CurrentId);

            if (results.Count == 0 && results.TotalResults > 0)
            {
                log.WarnFormat("[{2}] < results inconsistency, {0} entries whilst total results is {1}. Skipping analysis", results.Count, results.TotalResults, Task.CurrentId);
                metrics.Add(new ExceptionMetric(new Exception(
                    string.Format("results inconsistency, {0} entries whilst total results is {1}. Skipping analysis",
                                results.Count, results.TotalResults))));
                return metrics;
            }

            int i = int.Parse(fd.Filters.FirstOrDefault(f => f.Key == "queryId").Value);
            var os = queryFiltersTuple[i].Reference.Target.CreateOpenSearchableEntity();

            List<double> avaLatencies = new List<double>();

            foreach (IOpenSearchResultItem item in results.Items)
            {
                foreach (var filterDefinition in fd.GetFilters())
                {
                    if (filterDefinition.ItemValidator != null)
                    {
                        if (filterDefinition.ItemValidator.Invoke(item))
                            continue;
                        log.WarnFormat("[{2}] Non expected item {0} with filter {1}", item.Identifier, filterDefinition.Label, Task.CurrentId);
                        wrongResults++;
                    }
                    if (filterDefinition.ResultsValidator != null)
                    {
                        if (filterDefinition.ResultsValidator.Invoke(results))
                            continue;
                        log.WarnFormat("[{2}] Non expected results {0} with filter {1}", results.Identifier, filterDefinition.Label, Task.CurrentId);
                        wrongResults++;
                    }
                }
                IOpenSearchResultItem referenceItem = FindCorrespondingItem(item, os, null, out IOpenSearchResultCollection referenceResults);
                if (referenceItem == null)
                {
                    log.WarnFormat("Item {0} not found in reference target", item.Identifier);
                    continue;
                }
                DateTimeOffset creationDate;
                creationDate = item.PublishDate;
                var dateStrings = item.ElementExtensions.ReadElementExtensions<string>("creationDate", "http://www.terradue.com/");
                if (dateStrings != null && dateStrings.Count() > 0)
                {
                    DateTimeOffset.TryParse(dateStrings.FirstOrDefault(), System.Globalization.CultureInfo.InstalledUICulture, System.Globalization.DateTimeStyles.AssumeUniversal, out creationDate);
                }
                log.DebugFormat("Target item creation date ({0}): {1}", item.Id, creationDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                DateTimeOffset ingestionDate = DateTime.MinValue;
                dateStrings = referenceItem.ElementExtensions.ReadElementExtensions<string>("ingestionDate", "http://www.terradue.com/");
                if (dateStrings != null && dateStrings.Count() > 0)
                {
                    DateTimeOffset.TryParse(dateStrings.FirstOrDefault(), System.Globalization.CultureInfo.InstalledUICulture, System.Globalization.DateTimeStyles.AssumeUniversal, out ingestionDate);
                }
                log.DebugFormat("Reference item ingestion date ({0}): {1}", referenceItem.Identifier, ingestionDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                double latencySeconds = creationDate.Subtract(ingestionDate).TotalSeconds;
                log.DebugFormat("Latency (seconds) ({0}): {1}", item.Id, latencySeconds);

                avaLatencies.Add(latencySeconds);
                validatedResults++;
            }

            metrics.Add(new LongMetric(MetricName.wrongResultsCount, wrongResults, "#"));
            metrics.Add(new LongMetric(MetricName.totalValidatedResults, validatedResults, "#"));
            metrics.Add(new LongMetric(MetricName.totalReadResults, results.Items.Count(), "#"));
            if (avaLatencies.Count() > 0)
            {
                metrics.Add(new LongMetric(MetricName.avgDataAvailabilityLatency, (long)avaLatencies.Average(), "sec"));
                metrics.Add(new LongMetric(MetricName.maxDataAvailabilityLatency, (long)avaLatencies.Max(), "sec"));
            }
            else
            {
                metrics.Add(new LongMetric(MetricName.avgDataOperationalLatency, -1, "sec"));
                metrics.Add(new LongMetric(MetricName.maxDataOperationalLatency, -1, "sec"));
            }
            return metrics;
        }


        private IOpenSearchResultItem FindCorrespondingItem(IOpenSearchResultItem item, IOpenSearchable os, FiltersDefinition filters, out IOpenSearchResultCollection result)
        {
            if (filters == null) filters = new FiltersDefinition(item.Identifier);
            if (item.Identifier.Substring(0, 3) == "L1C") {   // for providers which use tile identifiers
                string tileIdentifier = item.Identifier.Substring(4, 6);
                DateTime startTime = item.FindStartDate();
                DateTime endTime = item.FindEndDate();
                if (endTime == startTime) endTime = endTime.AddDays(1);
                filters.AddFilter("uid", "{http://a9.com/-/opensearch/extensions/geo/1.0/}uid", String.Format("*{0}*", tileIdentifier), tileIdentifier, null, null);
                filters.AddFilter("start", "{http://a9.com/-/opensearch/extensions/time/1.0/}start", startTime.ToString("O"), startTime.ToString("O"), null, null);
                filters.AddFilter("stop", "{http://a9.com/-/opensearch/extensions/time/1.0/}end", endTime.ToString("O"), endTime.ToString("O"), null, null);
            
            } else if (item.Identifier.Substring(0, 2) == "S2" && item.Identifier.Contains(".")) {   // Tile identifier
                // e.g. S2A_OPER_MSI_L1C_TL_VGS1_20211112T190720_A033386_T13VCK_N03.01 -> S2A_MSIL1C_*_T13VCK_20211112T190720
                filters.AddFilter(
                    "uid",
                    "{http://a9.com/-/opensearch/extensions/geo/1.0/}uid",
                    String.Format("{0}_{1}{2}_*_{3}_{4}",
                        item.Identifier.Substring(0, 3),
                        item.Identifier.Substring(9, 3),
                        item.Identifier.Substring(13, 3),
                        item.Identifier.Substring(49, 6),
                        item.Identifier.Substring(25, 15)
                    ),
                    item.Identifier,
                    null,
                    null
                );

            } else {
                filters.AddFilter("uid", "{http://a9.com/-/opensearch/extensions/geo/1.0/}uid", item.Identifier, item.Identifier, null, null);
            }
            result = ose.Query(os, filters.GetNameValueCollection());

            IOpenSearchResultItem correspondingItem = result.Items.FirstOrDefault();

            if (correspondingItem != null && correspondingItem.Identifier == item.Identifier)
            {
                return correspondingItem;
            }
            else
            {
                foreach (var it in result.Items) {
                    log.DebugFormat("- ID: {0}", it.Identifier);
                }
                Match uidMatch = sentinel2UidRegex.Match(item.Identifier);
                if (!uidMatch.Success)
                {
                    uidMatch = sentinel2TileRegex.Match(item.Identifier);
                    if (!uidMatch.Success) return null;
                }

                string tile = uidMatch.Groups["tile"].Value;
                return result.Items.Where(i => i.Identifier.Contains(tile)).FirstOrDefault();
            }
        }
    }
}
