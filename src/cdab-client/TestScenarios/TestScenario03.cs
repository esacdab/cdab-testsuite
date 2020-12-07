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
    internal class TestScenario03 : IScenario
    {
        private TargetSiteWrapper target;
        private int load_factor;
        private ILog log;

        public TestScenario03(ILog log, TargetSiteWrapper target, int load_factor)
        {
            this.log = log;
            this.load_factor = load_factor;
            this.target = target;
        }

        public string Id => "TS03";

        public string Title => "Systematic periodic data search and related remote data download";

        internal static bool CheckCompatibility(TargetSiteWrapper target)
        {
            return target.Type == TargetType.DATAHUB || target.Type == TargetType.DIAS;
        }

        public IEnumerable<TestCase> CreateTestCases()
        {
            List<TestCase> _testCases = new List<TestCase>();

            List<IOpenSearchResultItem> foundItems;

            _testCases.Add(new TestCase203(log, target, load_factor, Mission.GenerateExistingDataDictionary(target), out foundItems));
            _testCases.Add(new TestCase303(log, target, load_factor, foundItems));

            return _testCases;
        }
    }
}