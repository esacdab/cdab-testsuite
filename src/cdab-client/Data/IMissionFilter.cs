using System;
using cdabtesttools.Config;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.Data
{
    public interface IMissionFilter
    {

        string FullName { get; }

        Func<IOpenSearchResultItem, bool> GetItemValidator(OpenSearchParameter parameter);

    }
}