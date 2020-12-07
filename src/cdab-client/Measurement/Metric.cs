using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace cdabtesttools.Measurement
{
    /// <summary>
    /// Abstract base class for the various metric types measured by the application.
    /// </summary>
    /// <typeparam name="T">The type that fits the unit of measurement of the metric.</typeparam>
    internal abstract class Metric<T>
    {
        private MetricName name;
        private T value;
        private string uom;

        [JsonConverter(typeof(StringEnumConverter))]
        public MetricName Name => name;

        public T Value => value;

        public string Uom => uom;

        public Metric(MetricName name, T value, string uom)
        {
            this.name = name;
            this.value = value;
            this.uom = uom;
        }
    }
}