using System;
using System.Linq;
using System.Net;
using cdabtesttools.Config;
using cdabtesttools.Data;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Asf;
using Terradue.OpenSearch.DataHub;
using Terradue.OpenSearch.DataHub.DHuS;
using Terradue.OpenSearch.DataHub.Dias;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.DataHub.Aws;
using Terradue.OpenSearch.DataHub.GoogleCloud;


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
        public TargetSiteWrapper(string name, TargetSiteConfiguration targetSiteConfig)
        {
            Name = name;
            this.targetSiteConfig = targetSiteConfig;
            ose = new Terradue.OpenSearch.Engine.OpenSearchEngine();
            ose.LoadPlugins();
            wrapper = CreateDataAccessWrapper(targetSiteConfig);
            target_type = InitType();
        }

        private TargetType InitType()
        {
            if (Wrapper.Settings.ServiceUrl.Host == "catalogue.onda-dias.eu")
            {
                log.DebugFormat("TARGET TYPE: DIAS");
                return TargetType.DIAS;
            }

            if (Wrapper.Settings.ServiceUrl.Host == "finder.creodias.eu")
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

            if (Wrapper.Settings.ServiceUrl.Host == "api.daac.asf.alaska.edu")
            {
                log.DebugFormat("TARGET TYPE: ASF");
                return TargetType.ASF;
            }

            if (Wrapper.Settings.ServiceUrl.Host.EndsWith("copernicus.eu"))
            {
                log.DebugFormat("TARGET TYPE: DATAHUB");
                return TargetType.DATAHUB;
            }

            if (Wrapper.Settings.ServiceUrl.Host.EndsWith("amazon.com"))
            {
                log.DebugFormat("TARGET TYPE: AMAZON");
                return TargetType.THIRDPARTY;
            }

            if (Wrapper.Settings.ServiceUrl.Host.EndsWith("googleapis.com") || Wrapper.Settings.ServiceUrl.Host.EndsWith("google.com"))
            {
                log.DebugFormat("TARGET TYPE: GOOGLE");
                return TargetType.THIRDPARTY;
            }

            return TargetType.UNKNOWN;
        }

        public static IDataHubSourceWrapper CreateDataAccessWrapper(TargetSiteConfiguration targetSiteConfig, FiltersDefinition filters = null)
        {

            var target_uri = targetSiteConfig.GetDataAccessUri();
            var target_creds = targetSiteConfig.GetDataAccessNetworkCredentials();

            if (target_creds == null)
                log.WarnFormat("Credentials are not set, target sites' services requiring credentials for data access will fail!");


            if (target_uri.Host == "catalogue.onda-dias.eu")
            {
                return new OndaDiasWrapper(new Uri(string.Format("https://catalogue.onda-dias.eu/dias-catalogue")), (NetworkCredential)target_creds, targetSiteConfig.Storage.ToOpenStackStorageSettings());
            }

            if (target_uri.Host == "finder.creodias.eu")
            {
                return new CreoDiasWrapper(target_creds, openStackStorageSettings: targetSiteConfig.Storage.ToOpenStackStorageSettings() );
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

            if (target_uri.Host == "api.daac.asf.alaska.edu")
            {
                return new AsfApiWrapper(target_uri, (NetworkCredential)target_creds);
            }

            if (target_uri.Host.EndsWith("copernicus.eu"))
            {
                if (filters != null && filters.GetFilters().Any(f => f.Key == "archiveStatus"))
                {
                    UriBuilder urib = new UriBuilder(target_uri);
                    urib.Path += target_uri.AbsolutePath.Replace("/search", "").TrimEnd('/').EndsWith("odata/v1") ? "" : "/odata/v1";
                    return new DHuSWrapper(urib.Uri, (NetworkCredential)target_creds);
                }
                return new DHuSWrapper(target_uri, (NetworkCredential)target_creds);
            }

            if (target_uri.Host.EndsWith("amazon.com"))
            {
                var searchWrapper = new DHuSWrapper(new Uri("https://scihub.copernicus.eu/apihub"), (NetworkCredential)target_creds);
                var amazonWrapper = new AmazonWrapper(targetSiteConfig.Data.S3SecretKey, targetSiteConfig.Data.S3KeyId, searchWrapper);
                return amazonWrapper;
            }

            if (target_uri.Host.EndsWith("googleapis.com") || target_uri.Host.EndsWith("google.com"))
            {
                var searchWrapper = new DHuSWrapper(new Uri("https://scihub.copernicus.eu/apihub"), (NetworkCredential)target_creds);
                var googleWrapper = new GoogleWrapper(targetSiteConfig.AccountFile, targetSiteConfig.ProjectId, searchWrapper);
                return googleWrapper;
            }

            return null;
        }

        internal IOpenSearchable CreateOpenSearchableEntity(FiltersDefinition filters = null, int maxRetries = 3)
        {
            OpenSearchableFactorySettings ossettings = new OpenSearchableFactorySettings(ose)
            {
                Credentials = Wrapper.Settings.Credentials,
                MaxRetries = maxRetries
            };
            IDataHubSourceWrapper wrapper = CreateDataAccessWrapper(TargetSiteConfig, filters);
            wrapper.Settings.MaxRetries = 3;
            return wrapper.CreateOpenSearchable(ossettings);
        }

        internal void AuthenticateRequest(HttpWebRequest request)
        {
            Wrapper.AuthenticateRequest(request);
        }
    }
}
