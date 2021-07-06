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
using Terradue.Metadata.EarthObservation.OpenSearch.Extensions;
using Terradue.OpenSearch.Result;

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
                // Generate all Data Collection Filter Definition for the set
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

        protected override IEnumerable<IMetric> AnalyzeResults(IOpenSearchResultCollection results, FiltersDefinition fd)
        {
            List<IMetric> metrics = new List<IMetric>();
            long wrongResults = 0;
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
                opsLatencies.Add(item.PublishDate.Subtract(item.FindStartDate()).TotalSeconds);
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
    }
}
