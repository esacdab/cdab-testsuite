using System.Collections.Generic;

namespace cdabtesttools.Config
{
    /// <summary>
    /// Data object class for the <em>data.sets.*</em> and <em>service_providers.*.data.catalogue.sets.*</em> nodes in the configuration YAML file.
    /// </summary>
    public class CatalogueSetConfiguration
    {
        public string ReferenceTargetSite { get; set; }

        public string ReferenceSetId { get; set; }

        public TargetCatalogueSetType Type { get; set; }

        public List<OpenSearchParameter> Parameters { get; set; }

        public Dictionary<string, DataCollectionDefinition> Collections { get; set; }

    }
}