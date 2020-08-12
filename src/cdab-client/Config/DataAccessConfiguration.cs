using System.Collections.Generic;

namespace cdabtesttools.Config
{
    public class DataAccessConfiguration
    {
        public string Url { get; set; }
        public string Credentials { get; set; }


        public TargetCatalogueConfiguration Catalogue { get; set; }

        public string S3SecretKey { get; set; }

        public string S3KeyId { get; set; }

        public DataAccessConfiguration() : this("https://scihub.copernicus.eu/dhus", null) { }

        public DataAccessConfiguration(string url, string credentials)
        {
            this.Url = url;
            this.Credentials = credentials;
        }
    }
}