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
using System.IO;
using log4net;
using YamlDotNet.Serialization;

namespace cdabtesttools.Config
{
    /// <summary>
    /// Data object class for the base node of the configuration YAML file.
    /// </summary>
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


        /// <summary>
        /// Gets the configuration of the target site with the specified name if it exists in the configuration YAML file.
        /// </summary>
        /// <param name="targetSiteName">The name of the target site as specified in the configuration YAML file.</param>
        /// <returns>The <see cref="TargetSiteConfiguration"> object representing the target site configuration.</returns>
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


        /// <summary>
        /// Loads the default configuration file <em>config.yaml</em>.
        /// </summary>
        /// <returns>An object representing the content of the configuration YAML file.</returns>
        private static Configuration LoadDefault()
        {
            FileInfo configFile = new FileInfo("config.yaml");
            return Load(configFile);
        }


        /// <summary>
        /// Loads the configuration file from the specified YAML file.
        /// </summary>
        /// <param name="configFile">A <see cref="FileInfo"/> object referring to the configuration file.</param>
        /// <returns>An object representing the content of the configuration YAML file.</returns>
        public static Configuration Load(FileInfo configFile)
        {
            ConfigurationFactory _configFactory = new ConfigurationFactory();
            return _configFactory.Load(configFile);
        }
    }
}