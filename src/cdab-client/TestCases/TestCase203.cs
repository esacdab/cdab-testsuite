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
using cdabtesttools.Data;
using cdabtesttools.Target;
using log4net;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Benchmarking;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.TestCases
{
    internal class TestCase203 : TestCase201
    {

        public TestCase203(ILog log, TargetSiteWrapper target, int load_factor, IEnumerable<Data.Mission> missions, out List<IOpenSearchResultItem> foundItems) :
            base(log, target, load_factor, missions, out foundItems)
        {
            Id = "TC203";
            Title = "Systematic Query";
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);
            var test = new ConcurrentQueue<FiltersDefinition>(DataHelper.GenerateBulkSystematicDataFiltersDefinition(target));
            queryFilters = test;
        }


    }
}