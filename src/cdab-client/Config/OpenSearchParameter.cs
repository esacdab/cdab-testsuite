namespace cdabtesttools.Config
{
    /// <summary>
    /// Data object class for the <em>data.sets.*.parameters</em>, <em>data.sets.*.collections.*.parameters</em>,
    /// <em>service_providers.*.data.catalogue.parameters</em>, <em>service_providers.*.data.catalogue.sets.*.parameters</em>
    /// and <em>service_providers.*.data.catalogue.sets.*.collections.*.parameters</em> nodes in the configuration YAML file.
    /// </summary>
    public class OpenSearchParameter
    {
        public string Key { get; set; }

        public string FullName { get; set; }

        public string Value { get; set; }

        public string Label { get; set; }
    }
}