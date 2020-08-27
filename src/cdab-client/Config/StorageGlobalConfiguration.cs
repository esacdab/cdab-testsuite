using System.Collections.Generic;

namespace cdabtesttools.Config
{
    public class StorageGlobalConfiguration
    {
        public string AuthUrl { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string ProjectId { get; set; }

        public string ProjectName { get; set; }

        public string UserDomainName { get; set; }

        public string S3KeyId { get; set; }

        public string S3SecretKey { get; set; }

        public string S3ServiceUrl { get; set; }

        public int MinUploadSize { get; set; }

        public int MaxUploadSize { get; set; }
    }
}