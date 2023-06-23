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
    internal class TestCase502 : TestCase201
    {
        private ConcurrentQueue<CrossCatalogueCoverageFiltersDefinition> queryFiltersTuple;

        public TestCase502(ILog log, TargetSiteWrapper target, int load_factor, out List<IOpenSearchResultItem> foundItems) :
            base(log, target, load_factor, null, out foundItems, true)
        {
            Id = "TC502";
            Title = "Target Local Data Coverage";
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);
            queryFiltersTuple = new ConcurrentQueue<CrossCatalogueCoverageFiltersDefinition>();
            foreach (var coverage in target.TargetSiteConfig.Data.Catalogue.Sets.Where(cs => cs.Value.Type == TargetCatalogueSetType.baseline))
            {
                foreach (var analysis in DataHelper.GenerateCrossCatalogueCoverageFiltersDefinition(coverage.Key, coverage.Value, target))
                {
                    if (target.TargetSiteConfig.Data.Catalogue.LocalParameters != null && target.TargetSiteConfig.Data.Catalogue.LocalParameters.Count() > 0)
                        analysis.Target.FiltersDefinition.AddFilters(target.TargetSiteConfig.Data.Catalogue.LocalParameters);
                    else
                        analysis.Target.FiltersDefinition.AddFilter("archiveStatus", "{http://a9.com/-/opensearch/extensions/eo/1.0/}statusSubType", "online", "Online", null, null);
                    queryFiltersTuple.Enqueue(analysis);
                }
            }

            load_factor = queryFiltersTuple.Count * 2;
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
                        return filters.Target.Target.CreateOpenSearchableEntity(filters.Target.FiltersDefinition, 3, true, true);
                    }).ContinueWith((request) =>
                        {
                            return MakeQuery(request.Result, filters.Target.FiltersDefinition);
                        });
                    _testUnits.Add(_testUnitOnline);

                    var _testUnit = previousTask[j].ContinueWith<IOpenSearchable>((task) =>
                    {
                        prepTask.Wait();
                        return filters.Reference.Target.CreateOpenSearchableEntity(filters.Reference.FiltersDefinition, 3, true, true);
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
                var testCaseResults = MeasurementsAnalyzer.GenerateTestCase502Result(this);
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
