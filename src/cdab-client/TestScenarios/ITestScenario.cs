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