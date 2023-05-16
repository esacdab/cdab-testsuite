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

Exception

If you modify this file, or any covered work, by linking or combining it with Terradue.OpenSearch.SciHub 
(or a modified version of that library), containing parts covered by the terms of CC BY-NC-ND 3.0, 
the licensors of this Program grant you additional permission to convey or distribute the resulting work.
*/

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using cdabtesttools.Config;
using cdabtesttools.Data;
// using cdabtesttools.SampleTarget;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Asf;
using Terradue.OpenSearch.DataHub;
using Terradue.OpenSearch.DataHub.DHuS;
using Terradue.OpenSearch.DataHub.Dias;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.DataHub.Aws;
using Terradue.OpenSearch.DataHub.GoogleCloud;
using Terradue.OpenSearch.DataHub.MicrosoftPlanetaryComputer;
using Terradue.OpenSearch.Usgs;

namespace cdabtesttools.Target
{
    /// <summary>
    /// Class providing functionality related to a target site.
    /// </summary>
    public class TargetSiteWrapper
    {

        private static log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private TargetType target_type;
        private IDataHubSourceWrapper wrapper;
        private Terradue.OpenSearch.Engine.OpenSearchEngine ose;
        private readonly TargetSiteConfiguration targetSiteConfig;

        /// <summary>
        /// Gets the type of this target site wrapper.
        /// </summary>
        /// <value>One of the values of the <see cref="cdabtesttools.Target.TargetType"/> enum.</value>
        public TargetType Type
        {
            get
            {
                return target_type;
            }
        }

        /// <summary>
        /// Gets a label representing this target site.
        /// </summary>
        public string Label => string.Format("{2} {0} [{1}]", Type, Wrapper.Settings.ServiceUrl.Host, Name);

        /// <summary>
        /// Gets the name of this target site.
        /// </summary>
        public string Name { get; set; }

        public OpenSearchEngine OpenSearchEngine => ose;

        public IDataHubSourceWrapper Wrapper => wrapper;

        public TargetSiteConfiguration TargetSiteConfig => targetSiteConfig;

        /// <summary>
        /// Creates a <see cref="cdabtesttools.Target.TargetSiteWrapper"/> instance.
        /// </summary>
        /// <param name="name">A name for the target site.</param>
        /// <param name="targetSiteConfig">The object representing the target site node from the configuration YAML file.</param>
        public TargetSiteWrapper(string name, TargetSiteConfiguration targetSiteConfig, bool enableDirectDataAccess = false)
        {
            Name = name;
            this.targetSiteConfig = targetSiteConfig;
            ose = new Terradue.OpenSearch.Engine.OpenSearchEngine();
            ose.RegisterExtension(new Terradue.OpenSearch.Engine.Extensions.AtomOpenSearchEngineExtension());
            ose.RegisterExtension(new Terradue.OpenSearch.GeoJson.Extensions.FeatureCollectionOpenSearchEngineExtension());
            wrapper = CreateDataAccessWrapper(targetSiteConfig, null, enableDirectDataAccess);
            target_type = InitType();
        }

        private TargetType InitType()
        {
            // Uncomment the following line for testing the sample target sites.
            // return TargetType.DIAS;

            if (Wrapper.Settings.ServiceUrl.Host == "catalogue.onda-dias.eu")
            {
                log.DebugFormat("TARGET TYPE: DIAS");
                return TargetType.DIAS;
            }

            if (Wrapper.Settings.ServiceUrl.Host == "finder.creodias.eu" || Wrapper.Settings.ServiceUrl.Host == "datahub.creodias.eu" || Wrapper.Settings.ServiceUrl.Host == "catalogue.dataspace.copernicus.eu" || Wrapper.Settings.ServiceUrl.Host == "finder.code-de.org")
            {
                log.DebugFormat("TARGET TYPE: DIAS");
                return TargetType.DIAS;
            }

            if (Wrapper.Settings.ServiceUrl.Host.Contains("mundiwebservices.com"))
            {
                log.DebugFormat("TARGET TYPE: DIAS");
                return TargetType.DIAS;
            }

            if (Wrapper.Settings.ServiceUrl.Host.Contains("sobloo.eu"))
            {
                log.DebugFormat("TARGET TYPE: DIAS");
                return TargetType.DIAS;
            }

            if (Wrapper.Settings.ServiceUrl.Host.Contains("wekeo.eu"))
            {
                log.DebugFormat("TARGET TYPE: DIAS");
                return TargetType.DIAS;
            }

            if (Wrapper.Settings.ServiceUrl.Host == "api.daac.asf.alaska.edu")
            {
                log.DebugFormat("TARGET TYPE: ASF");
                return TargetType.ASF;
            }

            if (Wrapper.Settings.ServiceUrl.Host == "m2m.cr.usgs.gov")
            {
                log.DebugFormat("TARGET TYPE: USGS");
                return TargetType.USGS;
            }

            if (Wrapper.Settings.ServiceUrl.Host.EndsWith("copernicus.eu") || Wrapper.Settings.ServiceUrl.AbsolutePath.Contains("/dhus"))
            {
                log.DebugFormat("TARGET TYPE: DATAHUB");
                return TargetType.DATAHUB;
            }

            if (Wrapper.Settings.ServiceUrl.Host.EndsWith("amazon.com") || Wrapper.Settings.ServiceUrl.Host.EndsWith("sentinel-hub.com"))
            {
                log.DebugFormat("TARGET TYPE: AMAZON");
                return TargetType.THIRDPARTY;
            }

            if (Wrapper.Settings.ServiceUrl.Host.EndsWith("googleapis.com") || Wrapper.Settings.ServiceUrl.Host.EndsWith("google.com"))
            {
                log.DebugFormat("TARGET TYPE: GOOGLE");
                return TargetType.THIRDPARTY;
            }

            if (Wrapper.Settings.ServiceUrl.Host.EndsWith("microsoft.com"))
            {
                log.DebugFormat("TARGET TYPE: MICROSOFT");
                return TargetType.THIRDPARTY;
            }
            

            return TargetType.UNKNOWN;
        }

        public static IDataHubSourceWrapper CreateDataAccessWrapper(TargetSiteConfiguration targetSiteConfig, FiltersDefinition filters = null, bool enableDirectDataAccess = false)
        {
            var target_uri = targetSiteConfig.GetDataAccessUri();
            var target_creds = targetSiteConfig.GetDataAccessNetworkCredentials();

            // Uncomment the following line for testing the sample target sites.
            // return new SampleWrapper(target_uri, target_creds);

            if (target_creds == null)
                log.WarnFormat("Credentials are not set, target sites' services requiring credentials for data access will fail!");


            if (target_uri.Host == "catalogue.dataspace.copernicus.eu" && target_uri.AbsolutePath.Contains("odata"))
            {
                CopernicusOdataWrapper copernicusOdataWrapper = new CopernicusOdataWrapper((NetworkCredential)target_creds, String.Format("https://catalogue.dataspace.copernicus.eu/odata/v1"));
                return copernicusOdataWrapper;

            }

            if (target_uri.Host == "catalogue.onda-dias.eu")
            {
                OndaDiasWrapper ondaDiasWrapper = new OndaDiasWrapper(new Uri(string.Format("https://catalogue.onda-dias.eu/dias-catalogue")), (NetworkCredential)target_creds, targetSiteConfig.Storage.ToOpenStackStorageSettings());
                ondaDiasWrapper.EnableDirectDataAccess = enableDirectDataAccess;
                return ondaDiasWrapper;

            }

            if (target_uri.Host == "finder.creodias.eu" || target_uri.Host == "datahub.creodias.eu" || target_uri.Host == "catalogue.dataspace.copernicus.eu")
            {
                CreoDiasWrapper creoDiasWrapper;
                if (targetSiteConfig.Data.Url != null)
                {
                    creoDiasWrapper = new CreoDiasWrapper(target_creds, osUrl: targetSiteConfig.Data.Url, openStackStorageSettings: targetSiteConfig.Storage.ToOpenStackStorageSettings() );
                }
                else
                {
                    creoDiasWrapper = new CreoDiasWrapper(target_creds, openStackStorageSettings: targetSiteConfig.Storage.ToOpenStackStorageSettings() );
                }
                creoDiasWrapper.EnableDirectDataAccess = enableDirectDataAccess;
                return creoDiasWrapper;
            }

            if (target_uri.Host == "finder.code-de.org")
            {
                CodeDeDiasWrapper codeDeDiasWrapper;
                if (targetSiteConfig.Data.Url != null)
                {
                    codeDeDiasWrapper = new CodeDeDiasWrapper(target_creds, osUrl: targetSiteConfig.Data.Url, openStackStorageSettings: targetSiteConfig.Storage.ToOpenStackStorageSettings() );
                }
                else
                {
                    codeDeDiasWrapper = new CodeDeDiasWrapper(target_creds, openStackStorageSettings: targetSiteConfig.Storage.ToOpenStackStorageSettings() );
                }
                codeDeDiasWrapper.EnableDirectDataAccess = enableDirectDataAccess;
                return codeDeDiasWrapper;
            }

            if (target_uri.Host.Contains("mundiwebservices.com"))
            {
                var mundiDiasWrapper = new MundiDiasWrapper(target_creds, openStackStorageSettings: targetSiteConfig.Storage.ToOpenStackStorageSettings() );
                mundiDiasWrapper.S3KeyId = targetSiteConfig.Data.S3KeyId;
                mundiDiasWrapper.S3SecretKey = targetSiteConfig.Data.S3SecretKey;
                return mundiDiasWrapper;
            }

            if (target_uri.Host.Contains("sobloo.eu"))
            {
                var soblooDiasWrapper = new SoblooDiasWrapper(target_creds);
                soblooDiasWrapper.S3StorageSettings = targetSiteConfig.Storage.ToS3StorageSettings();
                return soblooDiasWrapper;
            }

            if (target_uri.Host.Contains("wekeo.eu"))
            {
                var wekeoDiasWrapper = new WekeoDiasWrapper(target_creds, targetSiteConfig.Data.Url, "application/json", targetSiteConfig.Storage.ToOpenStackStorageSettings());
                if (targetSiteConfig.Data.Catalogue.LimitQuery != null && targetSiteConfig.Data.Catalogue.LimitQuery.Value)
                {
                    wekeoDiasWrapper.LimitQuery = true;

                    if (targetSiteConfig.Data.Catalogue.DefaultBoundingBox != null)
                    {
                        string[] boundingBoxStrParts = targetSiteConfig.Data.Catalogue.DefaultBoundingBox.Split(',');
                        if (boundingBoxStrParts.Length != 4) throw new Exception("Bounding box must contain 4 coordinates");
                        double[] boundingBox = new double[4];
                        for (int i = 0; i < 4; i++)
                        {
                            if (!System.Double.TryParse(boundingBoxStrParts[i], out boundingBox[i]))
                            {
                                throw new Exception("Bounding box must contain numeric values");
                            }
                        }
                        wekeoDiasWrapper.DefaultBoundingBox = boundingBox;
                    }
                    if (targetSiteConfig.Data.Catalogue.MaxAreaDegrees != null)
                    {
                        wekeoDiasWrapper.MaxAreaDegrees = targetSiteConfig.Data.Catalogue.MaxAreaDegrees.Value;
                    }

                    if (targetSiteConfig.Data.Catalogue.DefaultStartTime != null)
                    {
                        if (DateTime.TryParse(targetSiteConfig.Data.Catalogue.DefaultStartTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime dt))
                        {
                            wekeoDiasWrapper.DefaultStartTime = dt;
                        }
                        else
                        {
                            throw new Exception("The default start time for limited queries must be ISO-formatted");
                        }
                    }
                    if (targetSiteConfig.Data.Catalogue.DefaultEndTime != null)
                    {
                        if (DateTime.TryParse(targetSiteConfig.Data.Catalogue.DefaultEndTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime dt))
                        {
                            wekeoDiasWrapper.DefaultEndTime = dt;
                        }
                        else
                        {
                            throw new Exception("The default end time for limited queries must be ISO-formatted");
                        }
                    }
                    if (targetSiteConfig.Data.Catalogue.MaxPeriodSeconds != null)
                    {
                        wekeoDiasWrapper.MaxPeriodSeconds = targetSiteConfig.Data.Catalogue.MaxPeriodSeconds.Value;
                    }
                    if (targetSiteConfig.Data.Catalogue.QueryPollingInterval != null)
                    {
                        wekeoDiasWrapper.PollingInterval = targetSiteConfig.Data.Catalogue.QueryPollingInterval.Value;
                    }
                }

                return wekeoDiasWrapper;
            }

            if (target_uri.Host == "api.daac.asf.alaska.edu")
            {
                return new AsfApiWrapper(target_uri, (NetworkCredential)target_creds);
            }

            if (target_uri.Host == "m2m.cr.usgs.gov")
            {
                return new UsgsDataWrapper(new Uri(string.Format("https://m2m.cr.usgs.gov/api/api")), (NetworkCredential)target_creds);
            }

            if (target_uri.Host.EndsWith("copernicus.eu") || target_uri.AbsolutePath.EndsWith("/dhus"))
            {
                // OData API only for copernicus.eu, not other similar providers
                if (target_uri.Host.EndsWith("copernicus.eu") && filters != null && filters.GetFilters().Any(f => f.Key == "archiveStatus"))
                {
                    UriBuilder urib = new UriBuilder(target_uri);
                    urib.Path += target_uri.AbsolutePath.Replace("/search", "").TrimEnd('/').EndsWith("odata/v1") ? "" : "/odata/v1";
                    return new DHuSWrapper(urib.Uri, (NetworkCredential)target_creds);
                }
                return new DHuSWrapper(target_uri, (NetworkCredential)target_creds);
            }

            if (target_uri.Host.EndsWith("amazon.com"))
            {
                //var searchWrapper = new DHuSWrapper(new Uri("https://scihub.copernicus.eu/apihub"), (NetworkCredential)target_creds);
                //var amazonWrapper = new AmazonOldWrapper(targetSiteConfig.Data.S3SecretKey, targetSiteConfig.Data.S3KeyId, searchWrapper);
                var amazonWrapper = new AmazonStacWrapper(targetSiteConfig.Data.S3SecretKey, targetSiteConfig.Data.S3KeyId, (NetworkCredential)target_creds);
                amazonWrapper.AllowOpenSearch = targetSiteConfig.Data.Catalogue.AllowOpenSearch;
                return amazonWrapper;
            }

            if (target_uri.Host.EndsWith("googleapis.com") || target_uri.Host.EndsWith("google.com"))
            {
                //var searchWrapper = new DHuSWrapper(new Uri("https://scihub.copernicus.eu/apihub"), (NetworkCredential)target_creds);
                //var googleWrapper = new GoogleWrapper(targetSiteConfig.AccountFile, targetSiteConfig.ProjectId, searchWrapper);
                var googleWrapper = new GoogleWrapper(targetSiteConfig.AccountFile, targetSiteConfig.ProjectId, (NetworkCredential)target_creds, "https://cloud.google.com");
                return googleWrapper;
            }

            if (target_uri.Host.EndsWith("microsoft.com"))
            {
                var googleWrapper = new MicrosoftWrapper((NetworkCredential)target_creds, "https://planetarycomputer.microsoft.com");
                return googleWrapper;
            }

            return null;
        }

        internal IOpenSearchable CreateOpenSearchableEntity(FiltersDefinition filters = null, int maxRetries = 3, bool forceTotalResults = false)
        {
            IDataHubSourceWrapper wrapper = CreateDataAccessWrapper(TargetSiteConfig, filters, false);
            wrapper.Settings.MaxRetries = 3;

            OpenSearchableFactorySettings ossettings = new OpenSearchableFactorySettings(ose)
            {
                Credentials = (wrapper is CreoDiasWrapper ? null : Wrapper.Settings.Credentials),   // CREODIAS datahub does not tolerate unnecessary authorization header
                MaxRetries = maxRetries
            };

            if (forceTotalResults && wrapper is AmazonStacWrapper) {
                (wrapper as AmazonStacWrapper).ForceTotalResults = true;
            }
            if (forceTotalResults && wrapper is GoogleWrapper) {
                (wrapper as GoogleWrapper).ForceTotalResults = true;
            }
            if (forceTotalResults && wrapper is MicrosoftWrapper) {
                (wrapper as MicrosoftWrapper).ForceTotalResults = true;
            }

            return wrapper.CreateOpenSearchable(ossettings);
        }

        internal void AuthenticateRequest(HttpWebRequest request)
        {
            Wrapper.AuthenticateRequest(request);
        }
    }
}
