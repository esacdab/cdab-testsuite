using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terradue.Metadata.EarthObservation.OpenSearch.Extensions;
using Terradue.OpenSearch.DataHub;
using Terradue.OpenSearch.DataHub.Aws;
using Terradue.OpenSearch.DataHub.DHuS;
using Terradue.OpenSearch.Request;
using Terradue.OpenSearch.Result;
using Terradue.OpenSearch.Sentinel;
using Terradue.OpenSearch.Sentinel.Data;
using Terradue.ServiceModel.Ogc.Eop21;

namespace Terradue.OpenSearch.DataHub.Dias
{

    public class SampleWrapper : IDataHubSourceWrapper
    {

        private log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private DataHubWrapperSettings settings;
        private S3StorageSettings s3StorageSettings;

        public S3StorageSettings S3StorageSettings
        {
            get { return s3StorageSettings; }
            set { s3StorageSettings = value; }
        }

        public SampleWrapper(Uri osUri, ICredentials credentials = null, S3StorageSettings s3StorageSettings = null)
        {
            this.settings = new DataHubWrapperSettings(osUri, credentials);
            this.s3StorageSettings = s3StorageSettings;
        }

        public string Name => string.Format("Sample Opensearch ({0})", Settings.ServiceUrl);

        public DataHubWrapperSettings Settings => settings;

        public IOpenSearchable CreateOpenSearchable(OpenSearchableFactorySettings settings)
        {
            // Use either a metadata source that uses OpenSearch
            //return new SampleRealOpenSearchable(this, settings);
            // Use either a metadata source that uses OpenSearch
            return new SampleVirtualOpenSearchable(this, settings);
        }

        public ISentinelDataClient ForProduct(ISentinelProduct data)
        {
            throw new NotImplementedException();
        }

        public IDataHubSearchClient Search(string key)
        {
            throw new NotImplementedException();
        }

        public void AuthenticateRequest(HttpWebRequest request)
        {
            // Add authentication header or similar for download request, e.g.

            // request.Headers.Add("Authorization", "Apikey " + previouslyObtainedApiKey));
        }

        public HttpWebRequest CreateAvailabilityTestRequest()
        {
            HttpWebRequest request = WebRequest.CreateHttp(new Uri("https://www.terradue.com"));
            request.Method = "HEAD";
            request.Timeout = 60000;
            request.KeepAlive = false;
            request.Proxy = null;
            return request;
        }

        public IAssetAccess GetEnclosureAccess(IOpenSearchResultItem item)
        {
            EarthObservationType eop = item.GetEarthObservationProfile() as EarthObservationType;

            if (eop.EopMetaDataProperty.EarthObservationMetaData.statusSubType == StatusSubTypeValueEnumerationType.OFFLINE)
            {
                ProductArchivingStatus productArchivingStatus = ProductArchivingStatus.OFFLINE;
                throw new ProductArchivingStatusException(item, productArchivingStatus);
            }

            var enclosure = item.Links.FirstOrDefault(l => l.RelationshipType == "enclosure" && l.Uri != null);
            if (enclosure == null)
                throw new EntryPointNotFoundException(string.Format("No enclosure found for {0}", item.Identifier));

            HttpWebRequest request = WebRequest.CreateHttp(enclosure.Uri);
            request.Timeout = 30000;
            request.ReadWriteTimeout = 900000;
            request.KeepAlive = false;
            request.Proxy = null;
            AuthenticateRequest(request);

            return SingleEnclosureAccess.CreateHttp(request, item);
        }

        public IAssetAccess OrderProduct(IOpenSearchResultItem item)
        {
            // Implement this if ordering is supported, e.g.

            // log.DebugFormat("Ordering {0}...", item.Identifier);
            // return DownloadOrOrder(item);

            return null;
        }

        public IAssetAccess DownloadOrOrder(IOpenSearchResultItem item)
        {
            return null;
        }

        public IAssetAccess CheckOrder(string orderId, IOpenSearchResultItem item)
        {
            // Implement this if ordering is supported, e.g.

            // log.DebugFormat("Checking {0}...", item.Identifier);
            // return DownloadOrOrder(item);

            return null;
        }

        public IStorageClient CreateStorageClient()
        {
            // Implement this if object storage is supported

            return new AmazonStorageClient(s3StorageSettings.S3KeyId, s3StorageSettings.S3SecretKey, s3StorageSettings.S3ServiceUrl);
        }

    }
}