using Terradue.OpenSearch.DataHub;

namespace cdabtesttools.Config
{
    public static class ConfigurationExtensions
    {
        public static OpenStackStorageSettings ToOpenStackStorageSettings(this StorageGlobalConfiguration storageConfig) {
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

        public static S3StorageSettings ToS3StorageSettings(this StorageGlobalConfiguration storageConfig) {
            if ( storageConfig == null ) return null;
            return new S3StorageSettings() {
                S3KeyId = storageConfig.S3KeyId,
                S3SecretKey = storageConfig.S3SecretKey,
                S3ServiceUrl = storageConfig.S3ServiceUrl
            };
        }
    }
}
