/*
cdab-client is part of the software suite used to run Test Scenarios 
for bechmarking various Copernicus Data Provider targets.
    
    Copyright (C) 2020 Terradue Ltd, www.terradue.com
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published
    by the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terradue.Metadata.EarthObservation.OpenSearch.Extensions;
using Terradue.OpenSearch;
using Terradue.OpenSearch.DataHub;
using Terradue.OpenSearch.DataHub.Aws;
using Terradue.OpenSearch.DataHub.DHuS;
using Terradue.OpenSearch.Result;
using Terradue.OpenSearch.Sentinel;
using Terradue.OpenSearch.Sentinel.Data;
using Terradue.ServiceModel.Ogc.Eop21;

namespace cdabtesttools.SampleTarget
{

    public class SampleWrapper : IDataHubSourceWrapper
    {

        private log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private DataHubWrapperSettings settings;

        public S3StorageSettings S3StorageSettings { get; set; }

        public string Name => string.Format("Sample Opensearch ({0})", Settings.ServiceUrl);

        public DataHubWrapperSettings Settings => settings;


        public SampleWrapper(Uri osUri, ICredentials credentials = null, S3StorageSettings s3StorageSettings = null)
        {
            this.settings = new DataHubWrapperSettings(osUri, credentials);
            this.S3StorageSettings = s3StorageSettings;
        }


        public IOpenSearchable CreateOpenSearchable(OpenSearchableFactorySettings settings)
        {
            // Use either a metadata source that uses OpenSearch
            return new SampleOpenSearchable(this, settings);
            //return new SampleVirtualOpenSearchable(this, settings);
        }

        public HttpRequestMessage CreateAvailabilityTestRequest()
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, Settings.ServiceUrl);
            return request;
        }


        public void AuthenticateRequest(HttpRequestMessage request)
        {
            // Add authentication header or similar for download request, e.g.

            // request.Headers.Add("Authorization", "Apikey " + previouslyObtainedApiKey));
        }


        public IAssetAccess GetEnclosureAccess(IOpenSearchResultItem item)
        {
            EarthObservationType eop = item.GetEarthObservationProfile() as EarthObservationType;

            if (eop != null && eop.EopMetaDataProperty.EarthObservationMetaData.statusSubType == StatusSubTypeValueEnumerationType.OFFLINE)
            {
                ProductArchivingStatus productArchivingStatus = ProductArchivingStatus.OFFLINE;
                throw new ProductArchivingStatusException(item, productArchivingStatus);
            }

            var enclosure = item.Links.FirstOrDefault(l => l.RelationshipType == "enclosure" && l.Uri != null);
            if (enclosure == null)
                throw new EntryPointNotFoundException(string.Format("No enclosure found for {0}", item.Identifier));

            // The enclosue URI is replaced with a fixed resource here for testing purposes,
            // replace the following line with the commented line below to download the actual resource
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://www.terradue.com/wp-content/uploads/2017/03/home2-2.jpg"));
            AuthenticateRequest(request);

            // Possible classes that implement IAssetAccess:
            // * SingleEnclosureAccess (for a single HTTP or FTP resource)
            //   -> use SingleEnclosurAccess.CreateHttp or SingleEnclosureAccess.CreateFtp
            // * MultipleEnclosureAccess (for multiple HTTP resources, e.g. a list of individual files for a product)
            //   -> use MultipleEnclosureAccess.Create
            // * S3ObjectAccess (for resources on an S3 object storage)
            //   -> use S3ObjectAccess.Create
            // * LocalDirectoryAccess (for resources locally available via file system access, e.g. when the job runs on a cloud)
            //   -> use LocalDirectoryAccess.CreateFromDirectory or LocalDirectoryAccess.CreateFromFile

            return SingleEnclosureAccess.CreateHttp(request, item);
        }


        public IAssetAccess OrderProduct(IOpenSearchResultItem item)
        {
            // Implement this if ordering is supported, e.g.

            // log.DebugFormat("Ordering {0}...", item.Identifier);
            // return DownloadOrOrder(item);

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

            return new AmazonStorageClient(S3StorageSettings.S3KeyId, S3StorageSettings.S3SecretKey, S3StorageSettings.S3ServiceUrl);
        }


        public IDataHubSearchClient Search(string key)
        {
            throw new NotImplementedException();
        }


        public ISentinelDataClient ForProduct(ISentinelProduct data)
        {
            throw new NotImplementedException();
        }
    }
}