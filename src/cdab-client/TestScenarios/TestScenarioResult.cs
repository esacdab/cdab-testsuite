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
using cdabtesttools.TestCases;

namespace cdabtesttools.TestScenarios
{
    /// <summary>
    /// Represents metadata and the test case results of a test scenario execution.
    /// </summary>
    public class TestScenarioResult
    {
        public string JobName { get; set; }

        public string BuildNumber { get; set; }

        public string TestScenario { get; set; }

        public string TestSite { get; set; }

        public string TestTargetUrl { get; set; }

        public string TestTarget { get; set; }

        public string ZoneOffset { get; set; }

        public string HostName { get; set; }

        public string HostAddress { get; set; }

        public TestCaseResult[] TestCaseResults { get; set; }
    }
}
