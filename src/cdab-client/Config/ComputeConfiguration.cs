using System.Collections.Generic;

namespace cdabtesttools.Config
{
    /// <summary>
    /// Data object class for the <em>service_providers.*.compute</em> nodes in the configuration YAML file.
    /// </summary>
    public class ComputeConfiguration
    {
        public string AuthUrl { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string ProjectId { get; set; }

        public string ProjectName { get; set; }

        public string UserDomainName { get; set; }
    }
}