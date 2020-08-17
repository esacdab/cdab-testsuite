using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace cdabtesttools.TestCases
{
    public abstract class TestCase
    {
        public TestCase(string id, string title)
        {
            Id = id;
            Title = title;
            results = new List<TestUnitResult>();
        }

        public string Id { get; internal set; }

        public string Title { get; internal set; }

        public DateTimeOffset StartTime { get; internal set; }

        public DateTimeOffset EndTime { get; internal set; }

        public abstract void PrepareTest();

        public abstract IEnumerable<TestUnitResult> RunTestUnits(TaskFactory taskFactory, Task prepTask);

        public abstract TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks);

        protected List<TestUnitResult> results;

        public IEnumerable<TestUnitResult> Results => results;
    }
}
