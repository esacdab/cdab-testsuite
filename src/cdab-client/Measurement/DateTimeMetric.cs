using System;

namespace cdabtesttools.Measurement
{
    internal class DateTimeMetric : Metric<DateTimeOffset>, IMetric
    {
        public DateTimeMetric(MetricName name, DateTimeOffset value, string uom): base(name, value,uom) {}
    }
}