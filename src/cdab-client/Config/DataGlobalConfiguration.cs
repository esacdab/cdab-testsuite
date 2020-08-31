using System.Collections.Generic;

namespace cdabtesttools.Config
{
    /// <summary>
    /// Data object class for the <em>data</em> node in the configuration YAML file.
    /// </summary>
    public class DataGlobalConfiguration
    {
        public Dictionary<string, CatalogueSetConfiguration> Sets { get; set; }
    }
}