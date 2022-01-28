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
    internal class TestCase601 : TestCase201
    {
        public TestCase601(ILog log, TargetSiteWrapper target, int load_factor, out List<IOpenSearchResultItem> foundItems) :
            base(log, target, load_factor, null, out foundItems)
        {
            Id = "TC601";
            Title = "Data Operational Latency Analysis [including Time Critical]";
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);
            queryFilters = new ConcurrentQueue<FiltersDefinition>();
            // Get all Target Site catalogue sets configured as 'Baseline'
            foreach (var set in target.TargetSiteConfig.Data.Catalogue.Sets.Where(cs => cs.Value.Type == TargetCatalogueSetType.baseline))
            {
                foreach (var division in DataHelper.GenerateDataOperationalLatencyFiltersDefinition(set.Key, set.Value))
                {
                    queryFilters.Enqueue(division);
                }
            }
        }

        public override TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks)
        {
            List<IMetric> _testCaseMetrics = new List<IMetric>();

            try
            {
                results = tasks.Result.ToList();
                var testCaseResults = MeasurementsAnalyzer.GenerateTestCase601Result(this);
                testCaseResults.SearchFiltersDefinition = results.Select(tcr => tcr.FiltersDefinition).ToList();
                return testCaseResults;
            }
            catch (Exception e)
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
                                IOpenSearchable targetEntity = request.Result.Key;
                                return PollUntilAvailable(targetEntity, parameters);
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


        public TestUnitResult PollUntilAvailable(IOpenSearchable targetEntity, FiltersDefinition fd)
        {

            List<IMetric> metrics = new List<IMetric>();

            log.DebugFormat("[{1}] > Query {0} {2}...", fd.Name, Task.CurrentId, fd.Label);

            DateTime fromTime = DateTime.UtcNow;

            int latencyCheckOffset = (target.TargetSiteConfig.Data.Catalogue.LatencyCheckOffset != null ? target.TargetSiteConfig.Data.Catalogue.LatencyCheckOffset.Value : 0);
            if (fd.DataCollection != null && fd.DataCollection.LatencyCheckOffset != null) latencyCheckOffset = fd.DataCollection.LatencyCheckOffset.Value;

            if (latencyCheckOffset != 0)
            {
                fromTime = DateTime.UtcNow.AddSeconds(- latencyCheckOffset);
            }
        
            var parameters = fd.GetNameValueCollection();

            return catalogue_task_factory.StartNew(() =>
            {
                metrics.Add(new StringMetric(MetricName.dataCollectionDivision, fd.Label, "string"));
                if (true)
                {
                    List<double> opsLatencies = new List<double>();
                    long validatedResults = 0;

                    int latencyCheckMaxDuration = (target.TargetSiteConfig.Data.Catalogue.LatencyCheckMaxDuration != null ? target.TargetSiteConfig.Data.Catalogue.LatencyCheckMaxDuration.Value : 3600);
                    if (fd.DataCollection != null && fd.DataCollection.LatencyCheckMaxDuration != null) latencyCheckMaxDuration = fd.DataCollection.LatencyCheckMaxDuration.Value;
                    int latencyCheckInterval = (target.TargetSiteConfig.Data.Catalogue.LatencyCheckInterval != null ? target.TargetSiteConfig.Data.Catalogue.LatencyCheckInterval.Value : 600);
                    if (fd.DataCollection != null && fd.DataCollection.LatencyCheckInterval != null) latencyCheckInterval = fd.DataCollection.LatencyCheckInterval.Value;

                    log.DebugFormat("Latency handling for {0}: max duration: {1} s, check ever {2} s", fd.Label, latencyCheckMaxDuration, latencyCheckInterval);

                    DateTime maxEndTime = DateTime.UtcNow.AddSeconds(latencyCheckMaxDuration);
                    log.InfoFormat("Checking for new product in target until {0}", maxEndTime.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                    bool oneMissing = true;
                    try {
                        int iterationCount = 0;

                        while (oneMissing && DateTime.UtcNow < maxEndTime) {
                            iterationCount += 1;

                            oneMissing = false;

                            Stopwatch sw = new Stopwatch();
                            DateTimeOffset timeStart = DateTimeOffset.UtcNow;
                            IOpenSearchResultItem targetItem = null;
                            IOpenSearchResultCollection targetResults = null;
                            sw.Start();
                            try
                            {
                                targetItem = FindNewItem(
                                    targetEntity,
                                    fd,
                                    fromTime,
                                    out targetResults
                                );
                            }
                            catch (Exception e)
                            {
                                log.Warn("Error during target request for new item");
                                log.WarnFormat("Error message: {0}", e.Message);
                                log.WarnFormat("Skipping {0} {1}", fd.Name, fd.Label);
                                break;
                            }
                            var respTime = sw.ElapsedMilliseconds;
                            sw.Stop();
                            DateTimeOffset timeStop = DateTimeOffset.UtcNow;

                            if (targetItem == null)
                            {
                                log.Info("No new item available yet in target");
                                oneMissing = true;
                            }
                            else
                            {
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
                                log.DebugFormat("Target item estimated creation date ({0}): {1}", targetItem.Id, creationDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                                foreach (var link in targetItem.Links) {
                                    if (link != null && link.RelationshipType == "enclosure") {
                                        log.DebugFormat("  - Enclosure: {0}", link.Uri.AbsoluteUri);
                                    }
                                }

                                DateTimeOffset measurementDate = targetItem.FindStartDate();
                                log.DebugFormat("Item measurement date ({0}): {1}", targetItem.Id, measurementDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                                double latencySeconds = creationDate.Subtract(measurementDate).TotalSeconds;
                                log.DebugFormat("Latency (seconds) ({0}): {1}", targetItem.Id, latencySeconds);

                                opsLatencies.Add(latencySeconds);
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

                        metrics.Add(new LongMetric(MetricName.wrongResultsCount, 1 - validatedResults, "#"));
                        metrics.Add(new LongMetric(MetricName.totalValidatedResults, validatedResults, "#"));
                        metrics.Add(new LongMetric(MetricName.totalReadResults, 1, "#"));
                        
                        if (opsLatencies.Count() > 0)
                        {
                            metrics.Add(new LongMetric(MetricName.avgDataOperationalLatency, (long)opsLatencies.Average(), "sec"));
                            metrics.Add(new LongMetric(MetricName.maxDataOperationalLatency, (long)opsLatencies.Max(), "sec"));
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

            List<double> opsLatencies = new List<double>();

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
                DateTimeOffset creationDate;
                creationDate = item.PublishDate;
                var dateStrings = item.ElementExtensions.ReadElementExtensions<string>("creationDate", "http://www.terradue.com/");
                if (dateStrings != null && dateStrings.Count() > 0)
                {
                    DateTimeOffset.TryParse(dateStrings.FirstOrDefault(), System.Globalization.CultureInfo.InstalledUICulture, System.Globalization.DateTimeStyles.AssumeUniversal, out creationDate);
                }
                log.DebugFormat("Target item creation date ({0}): {1}", item.Id, creationDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                
                DateTimeOffset measurementDate = item.FindStartDate();
                log.DebugFormat("Item measurement date ({0}): {1}", item.Id, measurementDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                double latencySeconds = creationDate.Subtract(measurementDate).TotalSeconds;
                log.DebugFormat("Latency (seconds) ({0}): {1}", item.Id, latencySeconds);

                opsLatencies.Add(latencySeconds);
                validatedResults++;
            }

            metrics.Add(new LongMetric(MetricName.wrongResultsCount, wrongResults, "#"));
            metrics.Add(new LongMetric(MetricName.totalValidatedResults, validatedResults, "#"));
            metrics.Add(new LongMetric(MetricName.totalReadResults, results.Items.Count(), "#"));
            if (opsLatencies.Count() > 0)
            {
                metrics.Add(new LongMetric(MetricName.avgDataOperationalLatency, (long)opsLatencies.Average(), "sec"));
                metrics.Add(new LongMetric(MetricName.maxDataOperationalLatency, (long)opsLatencies.Max(), "sec"));
            }
            else
            {
                metrics.Add(new LongMetric(MetricName.avgDataOperationalLatency, -1, "sec"));
                metrics.Add(new LongMetric(MetricName.maxDataOperationalLatency, -1, "sec"));
            }
            return metrics;
        }


        private IOpenSearchResultItem FindNewItem(IOpenSearchable os, FiltersDefinition filters, DateTime fromTime, out IOpenSearchResultCollection result)
        {
            if (filters == null) filters = new FiltersDefinition("new_product_search");
            DateTime startTime = fromTime;
            DateTime endTime = fromTime.AddDays(30);
            if (endTime == startTime) endTime = endTime.AddDays(1);
            filters.RemoveFilter("{http://a9.com/-/opensearch/extensions/time/1.0/}start");
            filters.RemoveFilter("{http://a9.com/-/opensearch/extensions/time/1.0/}end");
            filters.AddFilter("start", "{http://a9.com/-/opensearch/extensions/time/1.0/}start", startTime.ToString("yyyy-MM-dd\\THH:mm:ss\\Z"), startTime.ToString("yyyy-MM-dd\\THH:mm:ss\\Z"), null, null);
            filters.AddFilter("stop", "{http://a9.com/-/opensearch/extensions/time/1.0/}end", endTime.ToString("yyyy-MM-dd\\THH:mm:ss\\Z"), endTime.ToString("yyyy-MM-dd\\THH:mm:ss\\Z"), null, null);

            result = ose.Query(os, filters.GetNameValueCollection());
            return result.Items.FirstOrDefault();
        }
    }
}
