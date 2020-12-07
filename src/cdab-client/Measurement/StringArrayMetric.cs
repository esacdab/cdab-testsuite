namespace cdabtesttools.Measurement
{
    internal class StringArrayMetric : Metric<string[]>, IMetric
    {
        public StringArrayMetric(MetricName name, string[] value, string uom) : base(name, value, uom) {}
    }
}