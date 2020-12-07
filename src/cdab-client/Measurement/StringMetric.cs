namespace cdabtesttools.Measurement
{
    internal class StringMetric : Metric<string>, IMetric
    {
        public StringMetric(MetricName name, string value, string uom) : base(name, value, uom) {}
    }
}