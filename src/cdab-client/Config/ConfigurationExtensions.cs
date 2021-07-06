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

using Terradue.OpenSearch.DataHub;

namespace cdabtesttools.Config
{
    /// <summary>
    /// Extensions for objects based on configuration YAML file.
    /// </summary>
    public static class ConfigurationExtensions
    {
        public static OpenStackStorageSettings ToOpenStackStorageSettings(this StorageConfiguration storageConfig) {
            if ( storageConfig == null ) return null;
            return new OpenStackStorageSettings(){
                IdentityApiUrl = storageConfig.AuthUrl,
                Password = storageConfig.Password,
                ProjectId = storageConfig.ProjectId,
                ProjectName = storageConfig.ProjectName,
                UserDomainName = storageConfig.UserDomainName,
                Username = storageConfig.Username
            };
        }

        public static S3StorageSettings ToS3StorageSettings(this StorageConfiguration storageConfig) {
            if ( storageConfig == null ) return null;
            return new S3StorageSettings() {
                S3KeyId = storageConfig.S3KeyId,
                S3SecretKey = storageConfig.S3SecretKey,
                S3ServiceUrl = storageConfig.S3ServiceUrl
            };
        }
    }
}
