using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cdabtesttools.Data;
using cdabtesttools.Measurement;

namespace cdabtesttools.TestCases
{
    public class TestCaseResult
    {

        private Collection<IMetric> metrics;
        DateTimeOffset start;

        public DateTimeOffset Start
        {
            set
            {
                start = value;
            }
        }

        DateTimeOffset end;

        public DateTimeOffset End
        {
            set
            {
                end = value;
            }
        }

        
        public TestCaseResult(string name, IEnumerable<IMetric> metrics, DateTimeOffset start, DateTimeOffset end)
        {
            this.end = end;
            this.start = start;
            this.metrics = new Collection<IMetric>(metrics.ToList());
            this.TestName = name;
        }

        public string TestName { get; set; }

        public string ClassName { get; set; }

        public string StartedAt {
            get
            {
                if (start.Ticks == 0)
                    return null;
                return start.ToString("O");
            }
        }

        public string EndedAt
        {
            get
            {
                if (end.Ticks == 0)
                    return null;
                return end.ToString("O");
            }
        }

        public long Duration
        {
            get
            {
                if (start.Ticks == 0 || end.Ticks == 0)
                    return 0;
                return (long)end.Subtract(start).TotalMilliseconds;
            }
        }

        public Collection<IMetric> Metrics
        {
            get
            {
                return metrics;
            }

            set
            {
                metrics = value;
            }
        }

        public List<FiltersDefinition> SearchFiltersDefinition
        {
            get;
            set;
        }
    }
}