using System;
using cdabtesttools.TestCases;

namespace cdabtesttools.TestScenarios
{
    public class TestScenarioResult
    {
        public string JobName { get; set; }

        public string BuildNumber { get; set; }

        public string TestScenario { get; set; }

        public string TestSite { get; set; }

        public string TestTargetUrl { get; set; }

        public string TestTarget { get; set; }

        public string ZoneOffset { get; set; }

        public string HostName { get; set; }

        public string HostAddress { get; set; }

        public TestCaseResult[] TestCaseResults { get; set; }
    }
}
