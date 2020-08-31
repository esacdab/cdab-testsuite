namespace cdabtesttools.Measurement
{
    internal class LongMetric : Metric<long>, IMetric
    {
        public LongMetric(MetricName name, long value, string uom) : base(name, value, uom) {}
    }
}