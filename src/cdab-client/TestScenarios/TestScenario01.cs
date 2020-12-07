using System;
using System.Collections.Generic;
using System.Net;
using cdabtesttools.Config;
using cdabtesttools.Data;
using cdabtesttools.Target;
using cdabtesttools.TestCases;
using log4net;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.TestScenarios
{
    internal class TestScenario01 : IScenario
    {
        private TargetSiteWrapper target;
        private int load_factor;
        private ILog log;

        public TestScenario01(ILog log, TargetSiteWrapper target, int load_factor)
        {
            this.log = log;
            this.load_factor = load_factor;
            this.target = target;
        }

        public string Id => "TS01";

        public string Title => "Simple data search and single download";

        internal static bool CheckCompatibility(TargetSiteWrapper target)
        {
            return target.Type == TargetType.DATAHUB || target.Type == TargetType.DIAS || target.Type == TargetType.THIRDPARTY;
        }

        public IEnumerable<TestCase> CreateTestCases()
        {
            List<TestCase> _testCases = new List<TestCase>();

            List<IOpenSearchResultItem> foundItems;

            _testCases.Add(new TestCase101(log, target, load_factor));
            _testCases.Add(new TestCase201(log, target, load_factor, Mission.GenerateExistingDataDictionary(target), out foundItems));
            _testCases.Add(new TestCase301(log, target, foundItems));

            return _testCases;
        }
    }
}
