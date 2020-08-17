namespace cdabtesttools.Measurement
{
    internal class LongArrayMetric : Metric<long[]>, IMetric
    {

        public LongArrayMetric(MetricName name, long[] value, string uom): base(name, value,uom)
        { }
    }
}