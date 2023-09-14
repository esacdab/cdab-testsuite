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
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using cdabtesttools.Config;
using cdabtesttools.Data;
using cdabtesttools.Measurement;
using cdabtesttools.Target;
using log4net;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Result;
using Terradue.OpenSearch.DataHub;

namespace cdabtesttools.TestCases
{
    internal class TestCase201 : TestCase
    {
        protected readonly TargetSiteWrapper target;
        protected int load_factor;
        protected readonly IEnumerable<Data.Mission> missions;
        protected readonly ILog log;
        private ServicePoint sp;
        protected OpenSearchEngine ose;
        protected readonly MaxParallelismTaskScheduler catalogue_scheduler;
        protected readonly TaskFactory catalogue_task_factory;
        protected ConcurrentQueue<FiltersDefinition> queryFilters = null;
        protected List<IOpenSearchResultItem> foundItems = null;
        protected bool ignoreEmptyResult = false;

        // Whether or not data collections not supported on the target are specially marked and filtered
        public virtual bool MarkUnsupportedData => false;


        public TestCase201(ILog log, TargetSiteWrapper target, int load_factor, IEnumerable<Data.Mission> missions, out List<IOpenSearchResultItem> foundItems, bool ignoreEmptyResult = false) :
            base("TC201", "Basic catalogue query")
        {
            this.log = log;
            this.load_factor = load_factor;
            this.missions = missions;
            this.target = target;
            this.sp = ServicePointManager.FindServicePoint(target.Wrapper.Settings.ServiceUrl);
            this.foundItems = new List<IOpenSearchResultItem>();
            this.ignoreEmptyResult = ignoreEmptyResult;
            foundItems = this.foundItems;
            ose = target.OpenSearchEngine;
            catalogue_scheduler = new MaxParallelismTaskScheduler(100);
            catalogue_task_factory = new TaskFactory(catalogue_scheduler);
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);

            var baselines = target.TargetSiteConfig.Data.Catalogue.Sets.Where(cs => cs.Value.Type == TargetCatalogueSetType.baseline).ToDictionary(c => c.Key, c => c.Value);

            // Workaround for non-compliant ONDA range syntax
            Func<ItemNumberRange, string> rangeReformatter = null;
            if (target.Wrapper.Settings.ServiceUrl.Host == "catalogue.onda-dias.eu") {
                rangeReformatter = (r) => {
                    return r.Formatter.Replace(",", " TO ");
                };
            }
            try {
                queryFilters = new ConcurrentQueue<FiltersDefinition>(Mission.ShuffleSimpleRandomFiltersCombination(missions, baselines, load_factor, rangeReformatter));
            } catch (Exception e) {
                throw;
            }

        }

        public override TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks)
        {
            List<IMetric> _testCaseMetrics = new List<IMetric>();

            try
            {
                results = tasks.Result.ToList();
                var testCaseResults = MeasurementsAnalyzer.GenerateTestCaseResult(this, new MetricName[]{
                    MetricName.avgResponseTime,
                    MetricName.peakResponseTime,
                    MetricName.errorRate,
                    MetricName.avgConcurrency,
                    MetricName.peakConcurrency,
                    MetricName.avgSize,
                    MetricName.maxSize,
                    MetricName.totalReadResults,
                    MetricName.maxTotalResults,
                    MetricName.resultsErrorRate,
                    MetricName.dataCollectionDivision
                }, tasks.Result.Count());
                testCaseResults.SearchFiltersDefinition = results.Select(tcr => tcr.FiltersDefinition).ToList();
                return testCaseResults;
            }
            catch (AggregateException e)
            {
                log.WarnFormat("Test Case Execution Error : {0}", e.InnerException.Message);
                log.WarnFormat("Stack Trace: {0}", e.InnerException.StackTrace);
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
                            return MakeQuery(request.Result.Key, request.Result.Value);
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

        public virtual TestUnitResult MakeQuery(IOpenSearchable entity, FiltersDefinition fd)
        {

            List<IMetric> metrics = new List<IMetric>();

            log.DebugFormat("[{1}] > Query {0} {2}...", fd.Name, Task.CurrentId, fd.Label);

            Stopwatch sw = new Stopwatch();
            sw.Start();
            DateTimeOffset timeStart = DateTimeOffset.UtcNow;

            var parameters = fd.GetNameValueCollection();

            return catalogue_task_factory.StartNew(() =>
            {
                return ose.Query(entity, parameters);
            }).ContinueWith<TestUnitResult>(task =>
            {
                var respTime = sw.ElapsedMilliseconds;
                Terradue.OpenSearch.Result.IOpenSearchResultCollection results = null;
                try
                {
                    results = task.Result;
                }
                catch (AggregateException e)
                {
                    log.DebugFormat("[{0}] < No results for {2}. Exception: {1}", Task.CurrentId, e.InnerException.Message, fd.Label);
                    if (e.InnerException is UnsupportedDataException && MarkUnsupportedData)
                    {
                        log.Debug("Data collection not supported by target");
                        metrics.Add(new LongMetric(MetricName.maxTotalResults, -2, "#"));
                        metrics.Add(new LongMetric(MetricName.totalReadResults, -2, "#"));
                    }
                    else
                    {
                        log.Debug(e.InnerException.StackTrace);
                        metrics.Add(new LongMetric(MetricName.maxTotalResults, -1, "#"));
                        metrics.Add(new LongMetric(MetricName.totalReadResults, -1, "#"));
                    }
                    metrics.Add(new ExceptionMetric(e.InnerException));
                }
                finally
                {
                    sw.Stop();
                }
                DateTimeOffset timeStop = DateTimeOffset.UtcNow;
                metrics.Add(new DateTimeMetric(MetricName.startTime, timeStart, "dateTime"));
                metrics.Add(new DateTimeMetric(MetricName.endTime, timeStop, "dateTime"));
                metrics.Add(new StringMetric(MetricName.dataCollectionDivision, fd.Label, "string"));
                if (results != null)
                {
                    foundItems.AddRange(results.Items);

                    long serializedSize = 0;
                    try
                    {
                        serializedSize = Encoding.Default.GetBytes(results.SerializeToString()).Length;
                    }
                    catch
                    {

                    }
                    var metricsArray = results.ElementExtensions.ReadElementExtensions<Terradue.OpenSearch.Benchmarking.Metrics>("Metrics", "http://www.terradue.com/metrics", Terradue.OpenSearch.Benchmarking.MetricFactory.Serializer);
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

                        log.DebugFormat("[{4}] < {0}/{1} entries for {6} {5}. {2}bytes in {3}ms", results.Count, results.TotalResults, _sizeMetric.Value, _responseTimeMetric.Value, Task.CurrentId, fd.Label, fd.Name);
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
                    metrics.Add(new LongMetric(MetricName.maxTotalResults, results.TotalResults, "#"));
                    metrics.Add(new LongMetric(MetricName.totalReadResults, results.Count, "#"));

                    if (results.Count == 0)
                    {
                        if ( results.TotalResults > 0 && !ignoreEmptyResult) {
                            log.WarnFormat("[{1}] no entries for {2} whilst {0} totalResult. This seems to be an error.", results.TotalResults, Task.CurrentId, fd.Label);
                            metrics.Add(new ExceptionMetric(new InvalidOperationException("No entries found")));
                        }
                    }
                    else
                    {
                        try
                        {
                            Stopwatch sw3 = new Stopwatch();
                            sw3.Start();
                            metrics.AddRange(AnalyzeResults(results, fd));
                            sw3.Stop();
                            metrics.Add(new LongMetric(MetricName.analysisTime, sw3.ElapsedMilliseconds, "msec"));
                        }
                        catch (Exception e)
                        {
                            metrics.Add(new ExceptionMetric(e));
                            log.ErrorFormat("[{0}] < Analysis failed for results {2} : {1}", Task.CurrentId, e.Message, fd.Label);
                            log.Debug(e.StackTrace);
                        }
                    }
                }

                return new TestUnitResult(metrics, fd);
            }).Result;
        }

        protected virtual IEnumerable<IMetric> AnalyzeResults(IOpenSearchResultCollection results, FiltersDefinition fd)
        {
            List<IMetric> metrics = new List<IMetric>();
            long errors = 0;

            if (results.Items.Count() > 0)
                log.DebugFormat("[{1}] Validating {0} result items...", results.Items.Count(), Task.CurrentId);

            foreach (var item in results.Items)
            {
                bool error = false;
                foreach (var filterDefinition in fd.GetFilters())
                {
                    if (filterDefinition.ItemValidator != null)
                    {
                        if (filterDefinition.ItemValidator.Invoke(item))
                            continue;
                        error = true;
                        log.WarnFormat("[{2}] Non expected item {0} with filter {1}", item.Identifier, filterDefinition.Label, Task.CurrentId);

                    }
                    if (filterDefinition.ResultsValidator != null)
                    {
                        if (filterDefinition.ResultsValidator.Invoke(results))
                            continue;
                        error = true;
                        log.WarnFormat("[{2}] Non expected results {0} with filter {1}", results.Identifier, filterDefinition.Label, Task.CurrentId);
                    }
                }
                if (error) errors++;
            }

            metrics.Add(new LongMetric(MetricName.wrongResultsCount, errors, "#"));

            return metrics;
        }
    }
}
