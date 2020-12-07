namespace cdabtesttools.Measurement
{
    /// <summary>
    /// Interface to be implemented by classes representing the metric types measured by the application.
    /// </summary>
    public interface IMetric
    {
        MetricName Name { get; }

        string Uom { get; }
    }
}