using System.Collections.Generic;
using cdabtesttools.Config;
using cdabtesttools.Target;
using log4net;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.TestCases
{
    internal class TestCase311 : TestCase301
    {
        public TestCase311(ILog log, TargetSiteWrapper target, List<IOpenSearchResultItem> foundItems) :
            base(log, target, foundItems)
        {
            this.Id = "TC311";
            this.Title = "Cloud Single Remote Download";
        }

    }
}