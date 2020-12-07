using System.Collections.Generic;

namespace cdabtesttools.Config
{
    /// <summary>
    /// Data object class for the <em>data.sets.*.collections</em> and <em>service_providers.*.data.catalogue.sets.*.collections</em> nodes in the configuration YAML file.
    /// </summary>
    public class DataCollectionDefinition
    {
        public string Label { get; set; }

        public List<OpenSearchParameter> Parameters { get; set; }

        
    }
}