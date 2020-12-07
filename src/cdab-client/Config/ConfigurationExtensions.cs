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
