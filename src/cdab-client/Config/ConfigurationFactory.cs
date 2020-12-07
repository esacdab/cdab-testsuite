using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace cdabtesttools.Config
{
    internal class ConfigurationFactory
    {
        private static log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        readonly IDeserializer deserializer;

        public ConfigurationFactory()
        {
            deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        internal Configuration Load(FileInfo config)
        {
            using (var reader = new StreamReader(config.OpenRead()))
            {
                return deserializer.Deserialize<Configuration>(reader);
            }
        }
    }
}