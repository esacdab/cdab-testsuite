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
using Terradue.Metadata.EarthObservation.OpenSearch.Extensions;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Benchmarking;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Result;
using Terradue.ServiceModel.Ogc.Eop21;
using Terradue.ServiceModel.Syndication;

namespace cdabtesttools.TestCases
{
    internal class TestCase302 : TestCase301
    {
        public TestCase302(ILog log, TargetSiteWrapper target, int load_factor, List<IOpenSearchResultItem> foundItems) :
            base(log, target, foundItems)
        {
            Id = "TC302";
            Title = "Multiple Remote Download";
            base.load_factor = target.TargetSiteConfig.MaxDownloadThread;
        }
    }
}