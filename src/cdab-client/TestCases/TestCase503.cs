using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using cdabtesttools.Config;
using cdabtesttools.Data;
using cdabtesttools.Measurement;
using cdabtesttools.Target;
using log4net;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Benchmarking;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.TestCases
{
    internal class TestCase503 : TestCase201
    {
        private ConcurrentQueue<CrossCatalogueCoverageFiltersDefinition> queryFiltersTuple;

        public TestCase503(ILog log, TargetSiteWrapper target, int load_factor, out List<IOpenSearchResultItem> foundItems) :
            base(log, target, load_factor, null, out foundItems, true)
        {
            Id = "TC503";
            Title = "Target Local Data Offer Consistency";
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);
            queryFiltersTuple = new ConcurrentQueue<CrossCatalogueCoverageFiltersDefinition>();
            foreach (var coverage in target.TargetSiteConfig.Data.Catalogue.Sets.Where(cs => cs.Value.Type == TargetCatalogueSetType.local))
            {
                foreach (var division in DataHelper.GenerateCrossCatalogueCoverageFiltersDefinition(coverage.Key, coverage.Value, target))
                {
                    queryFiltersTuple.Enqueue(division);
                }
            }
            load_factor = queryFiltersTuple.Count;
        }

        public override IEnumerable<TestUnitResult> RunTestUnits(TaskFactory taskFactory, Task prepTask)
        {
            try
            {
                prepTask.Wait();
            }
            catch (Exception e)
            {
                log.WarnFormat("Test Case Preparation Error : {0}", e.InnerException.Message);
                throw e;
            }

            StartTime = DateTimeOffset.UtcNow;

            List<Task<TestUnitResult>> _testUnits = new List<Task<TestUnitResult>>();
            Task[] previousTask = Array.ConvertAll(new int[1], i => prepTask);

            for (int i = 0; i < load_factor && !queryFiltersTuple.IsEmpty; i++)
            {
                for (int j = 0; j < 1 && !queryFiltersTuple.IsEmpty; j++)
                {
                    CrossCatalogueCoverageFiltersDefinition filters;
                    queryFiltersTuple.TryDequeue(out filters);
                    var _testUnitOnline = previousTask[j].ContinueWith<IOpenSearchable>((task) =>
                    {
                        prepTask.Wait();
                        return filters.Target.Target.CreateOpenSearchableEntity(filters.Target.FiltersDefinition);
                    }).ContinueWith((request) =>
                        {
                            return MakeQuery(request.Result, filters.Target.FiltersDefinition);
                        });
                    _testUnits.Add(_testUnitOnline);
                    
                    var _testUnit = previousTask[j].ContinueWith<IOpenSearchable>((task) =>
                    {
                        prepTask.Wait();
                        return filters.Reference.Target.CreateOpenSearchableEntity(filters.Reference.FiltersDefinition);
                    }).ContinueWith((request) =>
                        {
                            return MakeQuery(request.Result, filters.Reference.FiltersDefinition);
                        });
                    _testUnits.Add(_testUnit);
                    previousTask[j] = _testUnitOnline;
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
                foreach (var ex2 in ae.InnerExceptions)
                {
                    log.Debug(ex.Message);
                    log.Debug(ex.StackTrace);
                }
            }
            EndTime = DateTimeOffset.UtcNow;

            return _testUnits.Select(t => t.Result).Where(r => r != null);
        }

        public override TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks)
        {
            List<IMetric> _testCaseMetrics = new List<IMetric>();

            try
            {
                results = tasks.Result.ToList();
                var testCaseResults = MeasurementsAnalyzer.GenerateTestCase503Result(this);
                testCaseResults.SearchFiltersDefinition = results.Where(tcr => tcr.FiltersDefinition.Name.Contains("[target]")).Select(tcr => tcr.FiltersDefinition).ToList();
                return testCaseResults;
            }
            catch (AggregateException e)
            {
                log.WarnFormat("Test Case Execution Error : {0}", e.InnerException.Message);
                throw e;
            }
        }
    }
}