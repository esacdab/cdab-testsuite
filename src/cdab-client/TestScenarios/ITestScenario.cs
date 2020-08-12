using System.Collections.Generic;
using System.Threading.Tasks;
using cdabtesttools.TestCases;

namespace cdabtesttools.TestScenarios
{
    internal interface IScenario
    {
        string Id { get; }
        string Title { get; }

        IEnumerable<TestCase> CreateTestCases();
    }
}