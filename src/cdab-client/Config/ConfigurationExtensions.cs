using Terradue.OpenSearch.DataHub;

namespace cdabtesttools.Config
{
    public static class ConfigurationExtensions
    {
        public static OpenStackStorageSettings ToOpenStackStorageSettings(this StorageGlobalConfiguration storageConfig){
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
    }
}
