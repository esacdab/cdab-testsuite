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
    internal class TestScenario07 : IScenario
    {
        private TargetSiteWrapper target;
        private int load_factor;
        private ILog log;

        public TestScenario07(ILog log, TargetSiteWrapper target, int load_factor)
        {
            this.log = log;
            this.load_factor = load_factor;
            this.target = target;
        }

        public string Id => "TS07";

        public string Title => "Simple data upload to and download from cloud storage";

        internal static bool CheckCompatibility(TargetSiteWrapper target)
        {
            return target.Type == TargetType.DATAHUB || target.Type == TargetType.DIAS || target.Type == TargetType.THIRDPARTY;
        }

        public IEnumerable<TestCase> CreateTestCases()
        {
            List<TestCase> _testCases = new List<TestCase>();

            string storageName = GenerateName(10).ToLower();
            List<string> uploadedFiles = new List<string>();

            _testCases.Add(new TestCase701(log, target, load_factor, storageName, uploadedFiles));
            _testCases.Add(new TestCase702(log, target, load_factor, storageName, uploadedFiles));

            return _testCases;
        }

        public static string GenerateName(int len)
        {
            Random r = new Random();
            string[] consonants = { "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "sh", "zh", "t", "v", "w", "x" };
            string[] vowels = { "a", "e", "i", "o", "u", "ae", "y" };
            string Name = "";
            Name += consonants[r.Next(consonants.Length)].ToUpper();
            Name += vowels[r.Next(vowels.Length)];
            int b = 2; //b tells how many times a new letter has been added. It's 2 right now because the first two letters are already in the name.
            while (b < len)
            {
                Name += consonants[r.Next(consonants.Length)];
                b++;
                Name += vowels[r.Next(vowels.Length)];
                b++;
            }

            return Name;
        }
    }
}