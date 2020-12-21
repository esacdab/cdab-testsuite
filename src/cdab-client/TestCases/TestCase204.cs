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
