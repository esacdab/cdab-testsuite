using System.Collections.Generic;

namespace cdabtesttools.Config
{
    public class CatalogueBaseline
    {
        public string Name { get; set; }

        public Dictionary<string, DataCollectionDefinition> Collections { get; set; }
    }
}