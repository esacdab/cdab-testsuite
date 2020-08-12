using System.Collections.Generic;
using cdabtesttools.Data;
using cdabtesttools.Measurement;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.TestCases
{
    public class TestUnitResult
    {
        private List<IMetric> metrics;
        private readonly TestUnitResultStatus status;
        private FiltersDefinition fd;


        public List<IMetric> Metrics
        {
            get
            {
                return metrics;
            }
        }

        public FiltersDefinition FiltersDefinition { get => fd; set => fd = value; }

        public TestUnitResult(List<IMetric> metrics, TestUnitResultStatus status = TestUnitResultStatus.Complete)
        {
            this.metrics = metrics;
            this.status = status;
        }

        public TestUnitResult(List<IMetric> metrics, FiltersDefinition fd) : this(metrics)
        {
            this.fd = fd;
        }

        public object State { get; set; }
        public TestUnitResultStatus Status => status;
    }
}