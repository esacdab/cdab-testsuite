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

namespace cdabtesttools.TestScenarios {
    internal class TestScenario04 : IScenario {
        private TargetSiteWrapper target;
        private int load_factor;
        private ILog log;

        public TestScenario04 (ILog log, TargetSiteWrapper target, int load_factor) {
            this.log = log;
            this.load_factor = load_factor;
            this.target = target;
        }

        public string Id => "TS04";

        public string Title => "Offline data download";

        internal static bool CheckCompatibility (TargetSiteWrapper target) {
            return target.Type == TargetType.DATAHUB || target.Type == TargetType.DIAS;
        }

        public IEnumerable<TestCase> CreateTestCases () {
            if (target.Type == TargetType.DATAHUB) {
                UriBuilder urib = new UriBuilder (target.Wrapper.Settings.ServiceUrl);
                urib.Path = urib.Path.Replace ("/search", "");
                urib.Path += target.Wrapper.Settings.ServiceUrl.AbsolutePath.TrimEnd ('/').EndsWith ("odata/v1") ? "" : "/odata/v1";
                target.Wrapper.Settings.ServiceUrl = urib.Uri;
                target.Wrapper.Settings.MetadataModel = "dhus";
                target.Wrapper.Settings.ApiId = ApiId.DHUSV1;
            }

            List<TestCase> _testCases = new List<TestCase> ();

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

            _testCases.Add (new TestCase204 (log, target, load_factor, Mission.GenerateExistingDataDictionary (target), offlineDataStatus, out foundItems));
            _testCases.Add (new TestCase304 (log, target, load_factor, offlineDataStatus, foundItems));

            return _testCases;
        }
    }
}