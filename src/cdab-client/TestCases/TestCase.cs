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
using System.Threading.Tasks;

namespace cdabtesttools.TestCases
{
    /// <summary>
    /// Abstract base class for all test cases.
    /// </summary>
    public abstract class TestCase
    {
        protected List<TestUnitResult> results;

        /// <summary>
        /// Gets the ID of this test case.
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        /// Gets the title of this test case.
        /// </summary>
        public string Title { get; internal set; }

        /// <summary>
        /// Gets the start time of the execution of this testcase.
        /// </summary>
        public DateTimeOffset StartTime { get; internal set; }

        /// <summary>
        /// Gets the end time of the execution of this testcase.
        /// </summary>
        public DateTimeOffset EndTime { get; internal set; }

        /// <summary>
        /// Gets a list of the individual test units.
        /// </summary>
        public IEnumerable<TestUnitResult> Results => results;

        public TestCase(string id, string title)
        {
            Id = id;
            Title = title;
            results = new List<TestUnitResult>();
        }

        /// <summary>
        /// Performs operations necessary prior to the execution of this test case. 
        /// </summary>
        public abstract void PrepareTest();

        /// <summary>
        /// Runs this test case.
        /// </summary>
        /// <param name="taskFactory">The global <see cref="TaskFactory"/> to be used for the test run.</param>
        /// <param name="prepTask">The <see cref="Task"/> object representing the execution of the test preparation.</param>
        /// <returns>An <see cref="IEnumerable&lt;TestUnitResult&gt;"/> containing the metrics of the individual test units.</returns>
        public abstract IEnumerable<TestUnitResult> RunTestUnits(TaskFactory taskFactory, Task prepTask);

        /// <summary>
        /// Completes this test case and returns the obtained metrics.
        /// </summary>
        /// <param name="tasks">The tasks representing the individual test units.</param>
        /// <returns>A consolidated result of the metrics of the execution of this test case.</returns>
        public abstract TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks);

    }
}
