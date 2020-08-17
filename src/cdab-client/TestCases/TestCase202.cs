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
using cdabtesttools.Target;
using log4net;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Benchmarking;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.TestCases
{
    internal class TestCase202 : TestCase201
    {

        public TestCase202(ILog log, TargetSiteWrapper target, int load_factor, IEnumerable<Data.Mission> missions, out List<IOpenSearchResultItem> foundItems) :
            base(log, target, load_factor, missions, out foundItems)
        {
            Id = "TC202";
            Title = "Complex Query";
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);
            var baselines = target.TargetSiteConfig.Data.Catalogue.Sets.Where(cs => cs.Value.Type == TargetCatalogueSetType.baseline).ToDictionary(c => c.Key, c => c.Value);
            var test = new ConcurrentQueue<FiltersDefinition>(Mission.ShuffleComplexRandomFiltersCombination(missions, baselines, load_factor * target.TargetSiteConfig.MaxCatalogueThread));
            queryFilters = test;
        }


    }
}