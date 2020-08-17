using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using YamlDotNet.Serialization;

namespace cdabtesttools.Config
{
    public class Configuration
    {
        private static ILog log = LogManager.GetLogger(typeof(MainClass));
        private static Configuration current;

        public Configuration()
        {
            ServiceProviders = new Dictionary<string, TargetSiteConfiguration>();
        }

        public Dictionary<string, TargetSiteConfiguration> ServiceProviders { get; set; }

        public DataConfiguration CollectionConfiguration { get; set; }

        public GlobalConfiguration Global { get; set; }

        public DataGlobalConfiguration Data { get; set; }



        public TargetSiteConfiguration GetTargetSiteConfiguration(string targetSiteName)
        {
            if (Global != null && !string.IsNullOrEmpty(targetSiteName))
            {
                return ServiceProviders[targetSiteName];
            }
            throw new ArgumentException(string.Format("No target site with name '{0}' found.", targetSiteName));
        }

        public static Configuration Current
        {
            get
            {
                if (current == null)
                    current = LoadDefault();
                return current;
            }

            internal set
            {
                current = value;
            }
        }

        private static Configuration LoadDefault()
        {
            FileInfo configFile = new FileInfo("config.yaml");
            return Load(configFile);
        }

        public static Configuration Load(FileInfo configFile)
        {
            ConfigurationFactory _configFactory = new ConfigurationFactory();
            return _configFactory.Load(configFile);
        }
    }
}