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
            if (Data.Credentials.Contains(" "))   // multiple credentials are configured
                return null;
            if (Data.Credentials.Contains(":"))
            {
                var pair = Data.Credentials.Split(':');
                return new NetworkCredential(pair[0], pair[1]);
            }
            throw new NotImplementedException("Credentials format not supported");
        }

        internal NetworkCredential[] GetMultipleDataAccessNetworkCredentials()
        {
            if (string.IsNullOrEmpty(Data.Credentials))
                return null;
            string[] credList = Data.Credentials.Split(' ');
            List<NetworkCredential> credentialsList = new List<NetworkCredential>();
            foreach (string credStr in credList)
            {
                if (credStr.Contains(":"))
                {
                    var pair = credStr.Split(':');
                    credentialsList.Add(new NetworkCredential(pair[0], pair[1]));
                }
                else
                {
                    throw new NotImplementedException("Credentials format not supported");
                }
            }
            return credentialsList.ToArray();
        }

    }
}