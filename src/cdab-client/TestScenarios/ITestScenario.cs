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
using System.Threading.Tasks;
using cdabtesttools.TestCases;

namespace cdabtesttools.TestScenarios
{
    /// <summary>
    /// Interface to be implemented by test scenario classes.
    /// </summary>
    internal interface IScenario
    {
        /// <summary>
        /// Gets the ID of the test scenario, as specified in the service design document.
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// Gets a human-readable title of the test scenario.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// In implementing classes, composes the test scenarios by adding test cases to it.
        /// </summary>
        IEnumerable<TestCase> CreateTestCases();
    }
}
