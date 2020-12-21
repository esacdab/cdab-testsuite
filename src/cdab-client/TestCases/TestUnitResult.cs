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
