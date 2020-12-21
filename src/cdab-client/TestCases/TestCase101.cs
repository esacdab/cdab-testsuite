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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using cdabtesttools.Measurement;
using cdabtesttools.Target;
using log4net;

namespace cdabtesttools.TestCases
{
    internal class TestCase101 : TestCase
    {
        private TargetSiteWrapper target;
        private int load_factor;
        private ILog log;
        private ServicePoint sp;

        public TestCase101(ILog log, TargetSiteWrapper target, int load_factor) :
            base("TC101", "Service Reachability")
        {
            this.log = log;
            this.load_factor = load_factor;
            this.target = target;
            this.sp = ServicePointManager.FindServicePoint(target.Wrapper.Settings.ServiceUrl);
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);
        }

        public override IEnumerable<TestUnitResult> RunTestUnits(TaskFactory taskFactory, Task prepTask)
        {
            List<Task<TestUnitResult>> _testUnits = new List<Task<TestUnitResult>>();
            Task[] previousTask = Array.ConvertAll(new int[target.TargetSiteConfig.MaxCatalogueThread], mp => prepTask);

            int i = load_factor;
            while (i > 0)
            {
                for (int j = 0; j < target.TargetSiteConfig.MaxCatalogueThread && i > 0; j++)
                {
                    var _testUnit = previousTask[j].ContinueWith<HttpWebRequest>((task) => CreateRequest(), TaskContinuationOptions.AttachedToParent).
                        ContinueWith((request) => ReadResponse(request.Result));
                    _testUnits.Add(_testUnit);
                    previousTask[j] = _testUnit;
                    i--;
                }
            }
            Task.WaitAll(_testUnits.ToArray());
            EndTime = DateTimeOffset.UtcNow;
            return _testUnits.Select(task => task.Result);
        }

        public override TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks)
        {
            //Task.WaitAll(tasks.Cast<Task>().ToArray());

            List<IMetric> _testCaseMetric = new List<IMetric>();

            results = new List<TestUnitResult>();
            foreach (var task in tasks.Result)
            {
                try
                {
                    results.Add(task);
                }
                catch (AggregateException e)
                {
                    log.WarnFormat("Test Case Execution Error : {0}", e.InnerException.Message);
                    throw e;
                }
            }

            return MeasurementsAnalyzer.GenerateTestCaseResult(this, new MetricName[]{
                MetricName.avgResponseTime,
                MetricName.peakResponseTime,
                MetricName.errorRate,
                MetricName.avgConcurrency,
                MetricName.peakConcurrency
            }, tasks.Result.Count());
        }

        private HttpWebRequest CreateRequest()
        {
            if (StartTime.Ticks == 0)
                StartTime = DateTimeOffset.UtcNow;

            return target.Wrapper.CreateAvailabilityTestRequest();

            // HttpWebRequest request = WebRequest.CreateHttp(target.Wrapper.Settings.ServiceUrl);
            // request.Method = "HEAD";
            // request.Timeout = 60000;
            // request.KeepAlive = false;
            // request.Proxy = null;
            // return request;
        }

        private TestUnitResult ReadResponse(HttpWebRequest request)
        {
            List<IMetric> metrics = new List<IMetric>();

            log.DebugFormat("> HTTP HEAD {0} ...", request.RequestUri);

            Stopwatch sw = new Stopwatch();

            DateTimeOffset timeStart = DateTimeOffset.UtcNow;

            return Task.Factory.FromAsync((asyncCallback, state) =>
            {
                var asyncResult = request.BeginGetResponse(asyncCallback, state);
                sw.Start();
                metrics.Add(new LongMetric(MetricName.beginGetResponseTime, DateTime.UtcNow.Ticks, "ticks"));
                log.DebugFormat("Connected to {0}", request.RequestUri.Host);
                return asyncResult;
            }, request.EndGetResponse, null).ContinueWith(resp =>
            {
                sw.Stop();
                metrics.Add(new LongMetric(MetricName.endGetResponseTime, DateTime.UtcNow.Ticks, "ticks"));
                log.DebugFormat("Reply from {0}", request.RequestUri.Host);
                using (HttpWebResponse response = (HttpWebResponse)resp.Result)
                {
                    using (var responseStream = response.GetResponseStream())
                    {
                        log.DebugFormat("SP CC {0}/{1}", sp.CurrentConnections, sp.ConnectionLimit);
                        MemoryStream memoryStream = new MemoryStream();
                        responseStream.CopyTo(memoryStream);
                        DateTimeOffset timeStop = DateTimeOffset.UtcNow;
                        sw.Stop();
                        log.DebugFormat("< HTTP/{0} {1} {2} {3}ms", response.ProtocolVersion, response.StatusCode.ToString("D"), response.StatusDescription, sw.ElapsedMilliseconds);
                        metrics.Add(new LongMetric(MetricName.responseTime, sw.ElapsedMilliseconds, "ms"));
                        metrics.Add(new LongMetric(MetricName.size, response.ContentLength, "bytes"));
                        metrics.Add(new DateTimeMetric(MetricName.startTime, timeStart, "dateTime"));
                        metrics.Add(new DateTimeMetric(MetricName.endTime, timeStop, "dateTime"));
                    }
                }
                return new TestUnitResult(metrics);
            }).Result;
        }
    }
}
