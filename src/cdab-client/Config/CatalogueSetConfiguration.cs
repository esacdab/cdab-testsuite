using System.Collections.Generic;

namespace cdabtesttools.Config
{
    public class CatalogueSetConfiguration
    {
        public string ReferenceTargetSite { get; set; }

        public string ReferenceSetId { get; set; }

        public TargetCatalogueSetType Type { get; set; }

        public List<OpenSearchParameter> Parameters { get; set; }

        public Dictionary<string, DataCollectionDefinition> Collections { get; set; }

    }
}