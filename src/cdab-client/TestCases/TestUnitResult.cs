using System.Collections.Generic;
using cdabtesttools.Data;
using cdabtesttools.Measurement;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.TestCases
{
    /// <summary>
    /// Represents the result of the execution of an individual test unit that is part of a test case, such as the parallel execution of a single resource request.
    /// </summary>
    public class TestUnitResult
    {
        private List<IMetric> metrics;
        private readonly TestUnitResultStatus status;
        private FiltersDefinition fd;

        public object State { get; set; }

        public TestUnitResultStatus Status => status;

        public List<IMetric> Metrics => metrics;

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
    }
}