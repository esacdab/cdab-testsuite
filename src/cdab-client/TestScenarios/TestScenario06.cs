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
    internal class TestScenario06 : IScenario
    {
        private TargetSiteWrapper target;
        private int load_factor;
        private ILog log;

        public TestScenario06(ILog log, TargetSiteWrapper target, int load_factor)
        {
            this.log = log;
            this.load_factor = load_factor;
            this.target = target;
        }

        public string Id => "TS06";

        public string Title => "Data Latency Analysis";

        internal static bool CheckCompatibility(TargetSiteWrapper target)
        {
            return target.Type == TargetType.DATAHUB || target.Type == TargetType.DIAS;
        }

        public IEnumerable<TestCase> CreateTestCases()
        {
            List<TestCase> _testCases = new List<TestCase>();

            List<IOpenSearchResultItem> foundItems;

            _testCases.Add(new TestCase601(log, target, load_factor, out foundItems));
            _testCases.Add(new TestCase602(log, target, load_factor, out foundItems));

            return _testCases;
        }
    }
}