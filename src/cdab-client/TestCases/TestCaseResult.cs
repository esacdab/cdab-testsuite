/*
cdab-client is part of the software suite used to run Test Scenarios 
for bechmarking various Copernicus Data Provider targets.
    
Copyright (C) 2020 Terradue Ltd, www.terradue.com
    
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.
You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

/*
cdab-client is part of the software suite used to run Test Scenarios 
for bechmarking various Copernicus Data Provider targets.
    
Copyright (C) 2020 Terradue Ltd, www.terradue.com
    
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.
You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using cdabtesttools.Data;
using cdabtesttools.Measurement;

namespace cdabtesttools.TestCases
{
    /// <summary>
    /// Represents the consolidated result of the execution of a test case.
    /// </summary>
    public class TestCaseResult
    {
        private Collection<IMetric> metrics;
        DateTimeOffset start;
        DateTimeOffset end;

        public DateTimeOffset Start
        {
            set
            {
                start = value;
            }
        }

        public DateTimeOffset End
        {
            set
            {
                end = value;
            }
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
        
        public TestCaseResult(string name, IEnumerable<IMetric> metrics, DateTimeOffset start, DateTimeOffset end)
        {
            this.end = end;
            this.start = start;
            this.metrics = new Collection<IMetric>(metrics.ToList());
            this.TestName = name;
        }
    }
}
