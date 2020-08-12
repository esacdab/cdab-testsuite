namespace cdabtesttools.Measurement
{
    internal class DoubleMetric : Metric<double>, IMetric
    {

        public DoubleMetric(MetricName name, double value, string uom): base(name, value,uom)
        { }
    }
}