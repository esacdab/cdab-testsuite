using System.Collections.Generic;

namespace cdabtesttools.Config
{
    /// <summary>
    /// Data object class for the <em>service_providers.*.data.catalogue</em> nodes in the configuration YAML file.
    /// </summary>
    public class TargetCatalogueConfiguration
    {
        public Dictionary<string, CatalogueSetConfiguration> Sets { get; set; }

        public List<OpenSearchParameter> LocalParameters { get; set; }

    }
}