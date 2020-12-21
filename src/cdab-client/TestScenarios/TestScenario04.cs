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

Exception

If you modify this file, or any covered work, by linking or combining it with Terradue.OpenSearch.SciHub 
(or a modified version of that library), containing parts covered by the terms of CC BY-NC-ND 3.0, 
the licensors of this Program grant you additional permission to convey or distribute the resulting work.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using cdabtesttools.Config;
using cdabtesttools.Data;
using cdabtesttools.Target;
using cdabtesttools.TestCases;
using log4net;
using Newtonsoft.Json;
using Terradue.OpenSearch.DataHub.DHuS;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.TestScenarios
{
    internal class TestScenario04 : IScenario
    {
        private TargetSiteWrapper target;
        private int load_factor;
        private ILog log;

        public TestScenario04(ILog log, TargetSiteWrapper target, int load_factor)
        {
            this.log = log;
            this.load_factor = load_factor;
            this.target = target;
        }

        public string Id => "TS04";

        public string Title => "Offline data download";

        internal static bool CheckCompatibility(TargetSiteWrapper target)
        {
            return target.Type == TargetType.DATAHUB || target.Type == TargetType.DIAS;
        }

        public IEnumerable<TestCase> CreateTestCases()
        {
            if (target.Type == TargetType.DATAHUB)
            {
                UriBuilder urib = new UriBuilder(target.Wrapper.Settings.ServiceUrl);
                urib.Path = urib.Path.Replace("/search", "");
                urib.Path += target.Wrapper.Settings.ServiceUrl.AbsolutePath.TrimEnd('/').EndsWith("odata/v1") ? "" : "/odata/v1";
                target.Wrapper.Settings.ServiceUrl = urib.Uri;
                target.Wrapper.Settings.MetadataModel = "dhus";
                target.Wrapper.Settings.ApiId = ApiId.DHUSV1;
            }

            List<TestCase> _testCases = new List<TestCase>();

            List<IOpenSearchResultItem> foundItems = null;
            OfflineDataStatus offlineDataStatus = null;

            FileInfo offlineDataStatusFile = new FileInfo("offlineDataStatusFile.json");

            if (offlineDataStatusFile.Exists)
            {
                log.InfoFormat("Reading Offline Data Status file");
                using (var stream = offlineDataStatusFile.OpenRead())
                {
                    try
                    {
                        StreamReader sr = new StreamReader(stream);
                        offlineDataStatus = JsonConvert.DeserializeObject<OfflineDataStatus>(sr.ReadToEnd());
                    }
                    catch (Exception e)
                    {
                        log.WarnFormat("Error reading Offline Data Status file : {0}", e.Message);
                        log.Debug(e.StackTrace);
                    }
                }
            }
            if (offlineDataStatus == null || offlineDataStatus.OfflineData == null || offlineDataStatus.OfflineData.Count() == 0)
            {
                log.DebugFormat("No Previous Offline Data Status");
                offlineDataStatus = new OfflineDataStatus();
            }

            _testCases.Add(new TestCase204(log, target, load_factor, Mission.GenerateExistingDataDictionary(target), offlineDataStatus, out foundItems));
            _testCases.Add(new TestCase304(log, target, load_factor, offlineDataStatus, foundItems));

            return _testCases;
        }
    }
}
