using System.Collections.Generic;

namespace cdabtesttools.Config
{
    public class TargetCatalogueConfiguration
    {

        public Dictionary<string, CatalogueSetConfiguration> Sets { get; set; }

        public List<OpenSearchParameter> LocalParameters { get; set; }

    }
}