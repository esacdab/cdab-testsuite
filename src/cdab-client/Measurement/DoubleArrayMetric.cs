namespace cdabtesttools.Measurement
{
    internal class DoubleArrayMetric : Metric<double[]>, IMetric
    {
        public DoubleArrayMetric(MetricName name, double[] value, string uom): base(name, value,uom) {}
    }
}