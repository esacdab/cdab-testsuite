using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cdabtesttools.Config;
using cdabtesttools.Data;
using cdabtesttools.Target;
using log4net;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.TestCases
{
    internal class TestCase204 : TestCase201
    {
        private readonly OfflineDataStatus offlineDataStatus;

        public TestCase204(ILog log, TargetSiteWrapper target, int load_factor, IEnumerable<Data.Mission> missions, OfflineDataStatus offlineDataStatus, out List<IOpenSearchResultItem> foundItems) :
            base(log, target, load_factor, missions, out foundItems)
        {
            Id = "TC204";
            Title = "Offline Data Query";
            this.offlineDataStatus = offlineDataStatus;
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);
            log.DebugFormat("Generating filters for {0} offline data", load_factor * 10);
            queryFilters = new ConcurrentQueue<FiltersDefinition>(DataHelper.GenerateOfflineDataStatusFiltersDefinition(offlineDataStatus, target));
            for (int i = 0; queryFilters.Count < load_factor && i < 20; i++)
            {
                var baselines = target.TargetSiteConfig.Data.Catalogue.Sets.Where(cs => cs.Value.Type == TargetCatalogueSetType.baseline).ToDictionary(c => c.Key, c => c.Value);
                queryFilters.Enqueue(DataHelper.GenerateOfflineDataFiltersDefinition(load_factor * 10, missions, baselines));
            }
        }

        public override TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks)
        {
            UpdateOfflineDataStatus();

            return base.CompleteTest(tasks);
        }

        private void UpdateOfflineDataStatus()
        {
            foreach (var offlineDataStatusItem in offlineDataStatus.OfflineData.Where(di => di.TargetSiteName == target.Name))
            {
                offlineDataStatusItem.LastQueryUpdateDateTime = DateTime.UtcNow;
            }
        }
    }
}