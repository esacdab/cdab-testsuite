namespace cdabtesttools.Measurement
{
    public interface IMetric
    {
        MetricName Name { get; }

        string Uom { get; }
    }
}