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
using System.Linq;
using System.Threading.Tasks;
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
        private Dictionary<int, CrossCatalogueCoverageFiltersDefinition> queryFiltersTuple;

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

        protected override IEnumerable<IMetric> AnalyzeResults(IOpenSearchResultCollection results, FiltersDefinition fd)
        {
            List<IMetric> metrics = new List<IMetric>();
            long errors = 0;
            long validatedResults = 0;

            log.DebugFormat("[{1}] Validating and Analyzing {0} result items...", results.Items.Count(), Task.CurrentId);

            if (results.Count == 0 && results.TotalResults > 0)
            {
                log.WarnFormat("[{2}] < results inconsistency, {0} entries whilst total results is {1}. Skipping analysis", results.Count, results.TotalResults, Task.CurrentId);
                metrics.Add(new ExceptionMetric(new Exception(
                    string.Format("results inconsistency, {0} entries whilst total results is {1}. Skipping analysis",
                                results.Count, results.TotalResults))));
                return metrics;
            }

            List<IMetric> _testCaseMetrics = new List<IMetric>();

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
                        errors++;
                    }
                    if (filterDefinition.ResultsValidator != null)
                    {
                        if (filterDefinition.ResultsValidator.Invoke(results))
                            continue;
                        log.WarnFormat("[{2}] Non expected results {0} with filter {1}", results.Identifier, filterDefinition.Label, Task.CurrentId);
                        errors++;
                    }
                }
                IOpenSearchResultItem ref_item = FindReferenceItem(item, os);
                if (ref_item == null)
                {
                    log.WarnFormat("item {0} not found in reference target", item.Identifier);
                    continue;
                }
                DateTimeOffset onlineDate = item.PublishDate;
                log.DebugFormat("Publish date ({0}): {1}", item.Id, onlineDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                var onlineDateStrings = item.ElementExtensions.ReadElementExtensions<string>("onlineDate", "http://www.terradue.com/");
                if (onlineDateStrings != null && onlineDateStrings.Count() > 0)
                {
                    DateTimeOffset.TryParse(onlineDateStrings.FirstOrDefault(), System.Globalization.CultureInfo.InstalledUICulture, System.Globalization.DateTimeStyles.AssumeUniversal, out onlineDate);
                    log.DebugFormat("Online date ({0}): {1}", item.Id, onlineDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                }

                log.DebugFormat("Reference date ({0}): {1}", item.Id, ref_item.PublishDate.ToString("yyyy-MM-ddTHH:mm:ssZ"));

                avaLatencies.Add(onlineDate.Subtract(ref_item.PublishDate).TotalSeconds);
                log.DebugFormat("Latency (seconds) ({0}): {1}", item.Id, onlineDate.Subtract(ref_item.PublishDate).TotalSeconds);
                validatedResults++;
            }

            metrics.Add(new LongMetric(MetricName.wrongResultsCount, errors, "#"));
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

        private IOpenSearchResultItem FindReferenceItem(IOpenSearchResultItem item, IOpenSearchable os)
        {
            FiltersDefinition filters = new FiltersDefinition(item.Identifier);
            if (item.Identifier.Substring(0, 3) == "L1C") {   // for USGS which uses tile identifiers
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
            var result = ose.Query(os, filters.GetNameValueCollection());
            return result.Items.FirstOrDefault();
        }
    }
}
