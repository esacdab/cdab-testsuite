using System;
using System.Collections.Generic;
using System.Net;
using Terradue.OpenSearch.DataHub;

namespace cdabtesttools.Config
{
    /// <summary>
    /// Data object class for the <em>service_providers.*</em> nodes in the configuration YAML file.
    /// </summary>
    public class TargetSiteConfiguration
    {
        private int maxCatalogueThread = 2;
        private int maxDownloadThread = 2;
        private long maxDownloadSize = 1573741824;
        private int maxUploadThread = 2;

        public DataAccessConfiguration Data { get; set; }

        public StorageConfiguration Storage { get; set; }

        public ComputeConfiguration Compute { get; set; }

        public int MaxCatalogueThread { get => maxCatalogueThread; set => maxCatalogueThread = value; }

        public int MaxDownloadThread { get => maxDownloadThread; set => maxDownloadThread = value; }

        public int MaxUploadThread { get => maxUploadThread; set => maxUploadThread = value; }

        public long MaxDownloadSize { get => maxDownloadSize; internal set => maxDownloadSize = value; }

        public string AccountFile { get; set; }

        public string ProjectId { get; set; }

        public TargetSiteConfiguration() : this("https://scihub.copernicus.eu/dhus", null)
        {
        }

        public TargetSiteConfiguration(string url, string credentials)
        {
            Data = new DataAccessConfiguration(url, credentials);
        }

        public TargetSiteConfiguration(DataAccessConfiguration dataAccess)
        {
            this.Data = dataAccess;
        }

        internal Uri GetDataAccessUri()
        {
            return new Uri(Data.Url);
        }

        internal NetworkCredential GetDataAccessNetworkCredentials()
        {
            if (string.IsNullOrEmpty(Data.Credentials))
                return null;
            if (Data.Credentials.Contains(":"))
            {
                var _credstrings = Data.Credentials.Split(':');
                return new NetworkCredential(_credstrings[0], _credstrings[1]);
            }
            throw new NotImplementedException("Credentials format not supported");
        }
    }
}