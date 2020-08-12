using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace cdabtesttools.Measurement
{
    internal abstract class Metric<T>
    {
        private MetricName name;
        private T value;
        private string uom;

        public Metric(MetricName name, T value, string uom)
        {
            this.name = name;
            this.value = value;
            this.uom = uom;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public MetricName Name => name;

        public T Value => value;

        public string Uom => uom;
    }
}