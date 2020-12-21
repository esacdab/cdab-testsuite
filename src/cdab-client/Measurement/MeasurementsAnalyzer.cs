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

Exception

If you modify this file, or any covered work, by linking or combining it
with Terradue.OpenSearch.SciHub (or a modified version of that library),
containing parts covered by the terms of CC BY-NC-ND 3.0, the licensors
of this Program grant you additional permission to convey or distribute
the resulting work.
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
using System.Linq;
using System.Net;
using System.Net.Http;
using cdabtesttools.TestCases;
using log4net;
using Terradue.OpenSearch.DataHub;

namespace cdabtesttools.Measurement
{
    /// <summary>
    /// Functionality to generate test results for the various test cases.
    /// </summary>
    public static class MeasurementsAnalyzer
    {
        private static ILog log = LogManager.GetLogger(typeof(MeasurementsAnalyzer));

        public static TestCaseResult GenerateTestCaseResult(TestCase testCase, IEnumerable<MetricName> metricsToAnalyze, long tasksCount)
        {

            List<IMetric> metrics = new List<IMetric>();

            if (testCase.Results.Count() == 0)
            {
                return new TestCaseResult(testCase.Id, metrics, testCase.StartTime, testCase.EndTime);
                /*
                DoubleMetric _errorRate2 = new DoubleMetric(MetricName.errorRate, 100, "%");
                log.DebugFormat("Error Rate : {0}{1}", _errorRate2.Value, _errorRate2.Uom);
                metrics.Add(_errorRate2);
                var _tcr2 = new TestCaseResult(testCase.Id, metrics, testCase.StartTime, testCase.EndTime);
                _tcr2.ClassName = testCase.GetType().ToString();
                return _tcr2;
                */
            }

            var results = testCase.Results.Where(r => r != null);

            var _minStart = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.startTime).Cast<DateTimeMetric>().Select(m => m.Value.ToUnixTimeMilliseconds()).Min();
            var _maxStart = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.startTime).Cast<DateTimeMetric>().Select(m => m.Value.ToUnixTimeMilliseconds()).Max();
            var _minStop = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.endTime).Cast<DateTimeMetric>().Select(m => m.Value.ToUnixTimeMilliseconds()).Min();
            var _maxStop = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.endTime).Cast<DateTimeMetric>().Select(m => m.Value.ToUnixTimeMilliseconds()).Max();

            List<int> _concurrencySampling = new List<int>();

            try
            {
                var _minBeginGetResponseTime = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.beginGetResponseTime).Cast<LongMetric>().Select(m => m.Value).Min();
                var _maxBeginGetResponseTime = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.beginGetResponseTime).Cast<LongMetric>().Select(m => m.Value).Max();
                var _minEndGetResponseTime = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.endGetResponseTime).Cast<LongMetric>().Select(m => m.Value).Min();
                var _maxEndGetResponseTime = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.endGetResponseTime).Cast<LongMetric>().Select(m => m.Value).Max();

                // Sampling the concurrency (100 times)
                var _samplingPeriod = (_maxEndGetResponseTime - _minBeginGetResponseTime) / 100;
                for (long i = _minBeginGetResponseTime; i < _maxEndGetResponseTime; i += _samplingPeriod)
                {
                    int started = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.beginGetResponseTime).Cast<LongMetric>().Where(m => m.Value <= i).Count();
                    int ended = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.endGetResponseTime).Cast<LongMetric>().Where(m => m.Value <= i).Count();
                    int ongoing = started - ended;
                    _concurrencySampling.Add(ongoing);
                }
            }
            catch { }

            if (metricsToAnalyze.Contains(MetricName.avgResponseTime) && results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.responseTime).Count() > 0)
            {
                LongMetric _averageResponseTime = new LongMetric(MetricName.avgResponseTime,
                    (long)results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.responseTime).Cast<LongMetric>().Select(m => m.Value).Average(),
                     "ms");
                log.DebugFormat("Average Response Time : {0}{1}", _averageResponseTime.Value, _averageResponseTime.Uom);
                metrics.Add(_averageResponseTime);
            }

            if (metricsToAnalyze.Contains(MetricName.peakResponseTime) && results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.responseTime).Count() > 0)
            {
                LongMetric _peakResponseTime = new LongMetric(MetricName.peakResponseTime,
                    (long)results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.responseTime).Cast<LongMetric>().Select(m => m.Value).Max(),
                     "ms");
                log.DebugFormat("Peak Response Time : {0}{1}", _peakResponseTime.Value, _peakResponseTime.Uom);
                metrics.Add(_peakResponseTime);
            }

            if (metricsToAnalyze.Contains(MetricName.errorRate))
            {
                double _totalErrors = 0;
                foreach (var result in results)
                {
                    // Check if an exception was raised for this test unit
                    var exception = result.Metrics.FirstOrDefault(m => m.Name == MetricName.exception);
                    if (exception != null && exception is ExceptionMetric)
                    {
                        _totalErrors += 1;
                        continue;
                    }
                    // Check the number of retries for catalogue queries
                    var tryNumber = result.Metrics.FirstOrDefault(m => m.Name == MetricName.retryNumber);
                    var maxTryNumber = result.Metrics.FirstOrDefault(m => m.Name == MetricName.maxRetryNumber);
                    if (maxTryNumber != null && tryNumber != null && tryNumber is LongMetric)
                    {
                        _totalErrors += ((LongMetric)tryNumber).Value / ((LongMetric)maxTryNumber).Value;
                    }

                }
                DoubleMetric _errorRate = new DoubleMetric(MetricName.errorRate,
                        (_totalErrors / tasksCount) * 100,
                         "%");
                log.DebugFormat("Error Rate : {0}{1}", _errorRate.Value, _errorRate.Uom);
                metrics.Add(_errorRate);
            }

            if (metricsToAnalyze.Contains(MetricName.avgResponseTime) && _concurrencySampling.Count() > 0)
            {
                DoubleMetric _avgConcurrency = new DoubleMetric(MetricName.avgConcurrency,
                    Math.Round(_concurrencySampling.Average(), 2),
                     "#");
                log.DebugFormat("average Concurrency : {0}{1}", _avgConcurrency.Value, _avgConcurrency.Uom);
                metrics.Add(_avgConcurrency);
            }

            if (metricsToAnalyze.Contains(MetricName.avgResponseTime) && _concurrencySampling.Count() > 0)
            {
                LongMetric _peakConcurrency = new LongMetric(MetricName.peakConcurrency,
                    _concurrencySampling.Max(),
                     "#");
                log.DebugFormat("peak Concurrency : {0}{1}", _peakConcurrency.Value, _peakConcurrency.Uom);
                metrics.Add(_peakConcurrency);
            }

            if (metricsToAnalyze.Contains(MetricName.avgSize) && results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.size).Count() > 0)
            {
                LongMetric _avgSize = new LongMetric(MetricName.avgSize,
                    (long)results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.size).Cast<LongMetric>().Select(m => m.Value).Average(),
                     "bytes");
                log.DebugFormat("Average Size : {0}{1}", _avgSize.Value, _avgSize.Uom);
                metrics.Add(_avgSize);
            }

            if (metricsToAnalyze.Contains(MetricName.maxSize) && results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.size).Count() > 0)
            {
                LongMetric _maxSize = new LongMetric(MetricName.maxSize,
                    results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.size).Cast<LongMetric>().Select(m => m.Value).Max(),
                     "bytes");
                log.DebugFormat("Max Size : {0}{1}", _maxSize.Value, _maxSize.Uom);
                metrics.Add(_maxSize);
            }

            if (metricsToAnalyze.Contains(MetricName.totalSize) && results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.size).Count() > 0)
            {
                LongMetric _totalSize = new LongMetric(MetricName.totalSize,
                    results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.size).Cast<LongMetric>().Select(m => m.Value).Sum(),
                     "bytes");
                log.DebugFormat("Total Size : {0}{1}", _totalSize.Value, _totalSize.Uom);
                metrics.Add(_totalSize);
            }

            if (metricsToAnalyze.Contains(MetricName.throughput) && results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.size).Count() > 0)
            {
                var _totalBytes = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.size).Cast<LongMetric>().Select(m => m.Value).Sum();

                DoubleMetric _throughput = new DoubleMetric(MetricName.throughput,
                    Math.Round((double)_totalBytes / ((double)(_maxStop - _minStart) / 1000), 0),
                     "bytes/second");
                log.DebugFormat("Total Throughput : {0}{1}", _throughput.Value, _throughput.Uom);
                metrics.Add(_throughput);
            }


            if (metricsToAnalyze.Contains(MetricName.totalReadResults) && results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.totalReadResults).Count() > 0)
            {
                var _readResults = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.totalReadResults).Cast<LongMetric>().Select(m => m.Value).Sum();

                LongMetric _totalReadResultsCount = new LongMetric(MetricName.totalReadResults,
                    _readResults,
                     "#");
                log.DebugFormat("Total Read Results Count : {0}{1}", _totalReadResultsCount.Value, _totalReadResultsCount.Uom);
                metrics.Add(_totalReadResultsCount);
                if (metricsToAnalyze.Contains(MetricName.resultsErrorRate) && results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.wrongResultsCount).Count() > 0)
                {
                    var _resultsErrors = results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.wrongResultsCount).Cast<LongMetric>().Select(m => m.Value).Sum();
                    LongMetric _totalWrongResultsCount = new LongMetric(MetricName.totalWrongResults,
                        _resultsErrors,
                         "#");
                    log.DebugFormat("Total Wrong Results : {0}{1}", _totalWrongResultsCount.Value, _totalWrongResultsCount.Uom);

                    DoubleMetric _resultsErrorRate = new DoubleMetric(MetricName.resultsErrorRate,
                        Math.Round((_resultsErrors / (double)_readResults) * 100, 2),
                         "%");
                    log.DebugFormat("Results Error Rate : {0}{1}", _resultsErrorRate.Value, _resultsErrorRate.Uom);
                    metrics.Add(_resultsErrorRate);
                }
            }

            if (metricsToAnalyze.Contains(MetricName.maxTotalResults) && results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.maxTotalResults).Count() > 0)
            {
                LongMetric _maxTotalResults = new LongMetric(MetricName.maxTotalResults,
                    results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.maxTotalResults).Cast<LongMetric>().Select(m => m.Value).Max(),
                     "#");
                log.DebugFormat("Max Total Results : {0}{1}", _maxTotalResults.Value, _maxTotalResults.Uom);
                metrics.Add(_maxTotalResults);
            }

            if (metricsToAnalyze.Contains(MetricName.dataCollectionDivision) && results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.dataCollectionDivision).Count() > 0)
            {
                StringArrayMetric _dataCollectionDivisions = new StringArrayMetric(MetricName.dataCollectionDivision,
                    results.SelectMany(r => r.Metrics).Where(m => m.Name == MetricName.dataCollectionDivision).Cast<StringMetric>().Select(m => m.Value).ToArray(),
                "string");
                log.DebugFormat("Data Collection Division : {0}", string.Join(",", _dataCollectionDivisions.Value));
                metrics.Add(_dataCollectionDivisions);
            }

            if (metricsToAnalyze.Contains(MetricName.offlineDataAvailabilityLatency) && testCase is TestCase304)
            {
                TestCase304 testCase304 = testCase as TestCase304;
                List<TimeSpan> offlineDataAvailabilityLatencies = new List<TimeSpan>();
                foreach (var result in results)
                {
                    if (result.Status != TestUnitResultStatus.Complete)
                        continue;
                    var _httpStatus = result.Metrics.Where(m => m.Name == MetricName.httpStatusCode).Cast<StringMetric>().Select(m => m.Value);
                    int statusCode = int.Parse(_httpStatus.First().Split(':').First());
                    if (statusCode == 200)
                    {
                        var enclosureAccess = (IAssetAccess)result.State;
                        var offlineDataItem = testCase304.OfflineDataStatus.OfflineData.First(odi => odi.Identifier == enclosureAccess.SourceItem.Identifier);
                        offlineDataAvailabilityLatencies.Add(offlineDataItem.LastQueryUpdateDateTime.Subtract(offlineDataItem.FirstQueryDateTime));
                    }
                    else
                    {
                        offlineDataAvailabilityLatencies.Add(TimeSpan.FromSeconds(-1));
                    }
                }
                LongArrayMetric _offlineDataAvailabilityLatency = new LongArrayMetric(MetricName.offlineDataAvailabilityLatency,
                    offlineDataAvailabilityLatencies.Select(l => Convert.ToInt64(l.TotalSeconds)).ToArray(),
                     "seconds");
                log.DebugFormat("Offline Response Time : {0}{1}", string.Join(",", offlineDataAvailabilityLatencies), _offlineDataAvailabilityLatency.Uom);
                metrics.Add(_offlineDataAvailabilityLatency);
            }

            var _tcr = new TestCaseResult(testCase.Id, metrics, testCase.StartTime, testCase.EndTime);
            _tcr.ClassName = testCase.GetType().ToString();
            return _tcr;
        }

        internal static TestCaseResult GenerateTestCase501Result(TestCase501 testCase501)
        {
            List<IMetric> metrics = new List<IMetric>();

            if (testCase501.Results.Count() == 0)
            {
                DoubleMetric _errorRate2 = new DoubleMetric(MetricName.errorRate,
                100,
                 "%");
                log.DebugFormat("Error Rate : {0}{1}", _errorRate2.Value, _errorRate2.Uom);
                metrics.Add(_errorRate2);
                var _tcr2 = new TestCaseResult(testCase501.Id, metrics, testCase501.StartTime, testCase501.EndTime);
                _tcr2.ClassName = testCase501.GetType().ToString();
                return _tcr2;
            }

            Dictionary<string, double> catalogueCoverages = new Dictionary<string, double>();
            Dictionary<string, long> targetTotalResults = new Dictionary<string, long>();
            Dictionary<string, long> refTotalResults = new Dictionary<string, long>();

            for (int i = 0; i < testCase501.Results.Count(); i += 2)
            {
                var testUnitRef = testCase501.Results.ElementAt(i);
                var testUnit = testCase501.Results.ElementAt(i + 1);

                long totalResultsRef = testUnitRef.Metrics.Where(m => m.Name == MetricName.maxTotalResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                long totalResults = testUnit.Metrics.Where(m => m.Name == MetricName.maxTotalResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();

                double pct = -1;

                if (totalResultsRef > 0 && totalResults >= 0)
                {
                    pct = Math.Round((totalResults / (double)totalResultsRef) * 100, 2);
                }

                string dataCollectionDivision = testUnit.Metrics.Where(m => m.Name == MetricName.dataCollectionDivision).Cast<StringMetric>().Select(m => m.Value).FirstOrDefault();

                catalogueCoverages.Add(dataCollectionDivision, pct);
                targetTotalResults.Add(dataCollectionDivision, totalResults);
                refTotalResults.Add(dataCollectionDivision, totalResultsRef);
            }

            StringArrayMetric _dataCollectionDivisions = new StringArrayMetric(MetricName.dataCollectionDivision,
                    catalogueCoverages.Keys.ToArray(),
                     "string");
            log.DebugFormat("Data Collection Division : {0}{1}", string.Join(",", _dataCollectionDivisions.Value), _dataCollectionDivisions.Uom);
            metrics.Add(_dataCollectionDivisions);

            LongArrayMetric _targetTotalResults = new LongArrayMetric(MetricName.totalResults,
                    targetTotalResults.Values.ToArray(),
                     "#");
            log.DebugFormat("Target Total Results : {0}{1}", string.Join(",", _targetTotalResults.Value), _targetTotalResults.Uom);
            metrics.Add(_targetTotalResults);

            LongArrayMetric _refTotalResults = new LongArrayMetric(MetricName.totalReferenceResults,
                    refTotalResults.Values.ToArray(),
                     "#");
            log.DebugFormat("Reference Total Results : {0}{1}", string.Join(",", _refTotalResults.Value), _refTotalResults.Uom);
            metrics.Add(_refTotalResults);

            DoubleArrayMetric _coverages = new DoubleArrayMetric(MetricName.catalogueCoverage,
                    catalogueCoverages.Values.ToArray(),
                     "%");
            log.DebugFormat("Catalogue Coverages : {0}{1}", string.Join(",", _coverages.Value), _coverages.Uom);
            metrics.Add(_coverages);

            var _tcr = new TestCaseResult(testCase501.Id, metrics, testCase501.StartTime, testCase501.EndTime);
            _tcr.ClassName = testCase501.GetType().ToString();
            return _tcr;
        }

        internal static TestCaseResult GenerateTestCase502Result(TestCase502 testCase502)
        {
            List<IMetric> metrics = new List<IMetric>();

            if (testCase502.Results.Count() == 0)
            {
                DoubleMetric _errorRate2 = new DoubleMetric(MetricName.errorRate,
                100,
                 "%");
                log.DebugFormat("Error Rate : {0}{1}", _errorRate2.Value, _errorRate2.Uom);
                metrics.Add(_errorRate2);
                var _tcr2 = new TestCaseResult(testCase502.Id, metrics, testCase502.StartTime, testCase502.EndTime);
                _tcr2.ClassName = testCase502.GetType().ToString();
                return _tcr2;
            }

            Dictionary<string, double> onlinePct = new Dictionary<string, double>();
            Dictionary<string, long> targetTotalResultsNum = new Dictionary<string, long>();
            Dictionary<string, long> refTotalResultsNum = new Dictionary<string, long>();

            for (int i = 0; i < testCase502.Results.Count(); i += 2)
            {
                var testUnitOnline = testCase502.Results.ElementAt(i);
                var testUnit = testCase502.Results.ElementAt(i + 1);

                long targetTotalResults = testUnitOnline.Metrics.Where(m => m.Name == MetricName.maxTotalResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                long refTotalResults = testUnit.Metrics.Where(m => m.Name == MetricName.maxTotalResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();

                string dataCollectionDivision = testUnit.Metrics.Where(m => m.Name == MetricName.dataCollectionDivision).Cast<StringMetric>().Select(m => m.Value).FirstOrDefault();

                double pct = -1;

                if (refTotalResults > 0 && targetTotalResults >= 0)
                {
                    pct = Math.Round((targetTotalResults / (double)refTotalResults) * 100, 2);
                }

                onlinePct.Add(dataCollectionDivision, pct);
                targetTotalResultsNum.Add(dataCollectionDivision, targetTotalResults);
                refTotalResultsNum.Add(dataCollectionDivision, refTotalResults);
            }

            StringArrayMetric _dataCollectionDivisions = new StringArrayMetric(MetricName.dataCollectionDivision,
                    onlinePct.Keys.ToArray(),
                     "string");
            log.DebugFormat("Data Collection Division : {0}{1}", string.Join(",", _dataCollectionDivisions.Value), _dataCollectionDivisions.Uom);
            metrics.Add(_dataCollectionDivisions);

            LongArrayMetric _targetTotalResults = new LongArrayMetric(MetricName.totalResults,
                    targetTotalResultsNum.Values.ToArray(),
                     "#");
            log.DebugFormat("Target Total Results : {0}{1}", string.Join(",", _targetTotalResults.Value), _targetTotalResults.Uom);
            metrics.Add(_targetTotalResults);

            LongArrayMetric _refTotalResults = new LongArrayMetric(MetricName.totalReferenceResults,
                    refTotalResultsNum.Values.ToArray(),
                     "#");
            log.DebugFormat("Reference Total Results : {0}{1}", string.Join(",", _refTotalResults.Value), _refTotalResults.Uom);
            metrics.Add(_refTotalResults);

            DoubleArrayMetric _dataCoverage = new DoubleArrayMetric(MetricName.dataCoverage,
                    onlinePct.Values.ToArray(),
                     "%");
            log.DebugFormat("Data Coverages : {0}{1}", string.Join(",", _dataCoverage.Value), _dataCoverage.Uom);
            metrics.Add(_dataCoverage);

            var _tcr = new TestCaseResult(testCase502.Id, metrics, testCase502.StartTime, testCase502.EndTime);
            _tcr.ClassName = testCase502.GetType().ToString();
            return _tcr;
        }

        internal static TestCaseResult GenerateTestCase503Result(TestCase503 testCase503)
        {
            List<IMetric> metrics = new List<IMetric>();

            if (testCase503.Results.Count() == 0)
            {
                DoubleMetric _errorRate2 = new DoubleMetric(MetricName.errorRate,
                100,
                 "%");
                log.DebugFormat("Error Rate : {0}{1}", _errorRate2.Value, _errorRate2.Uom);
                metrics.Add(_errorRate2);
                var _tcr2 = new TestCaseResult(testCase503.Id, metrics, testCase503.StartTime, testCase503.EndTime);
                _tcr2.ClassName = testCase503.GetType().ToString();
                return _tcr2;
            }

            Dictionary<string, double> onlinePct = new Dictionary<string, double>();
            Dictionary<string, long> targetTotalResultsNum = new Dictionary<string, long>();
            Dictionary<string, long> refTotalResultsNum = new Dictionary<string, long>();

            for (int i = 0; i < testCase503.Results.Count(); i += 2)
            {
                var testUnitOnline = testCase503.Results.ElementAt(i);
                var testUnit = testCase503.Results.ElementAt(i + 1);

                long targetTotalResults = testUnitOnline.Metrics.Where(m => m.Name == MetricName.maxTotalResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                long refTotalResults = testUnit.Metrics.Where(m => m.Name == MetricName.maxTotalResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();

                string dataCollectionDivision = testUnit.Metrics.Where(m => m.Name == MetricName.dataCollectionDivision).Cast<StringMetric>().Select(m => m.Value).FirstOrDefault();

                double pct = -1;

                if (refTotalResults > 0 && targetTotalResults >= 0)
                {
                    pct = Math.Round((targetTotalResults / (double)refTotalResults) * 100, 2);
                }

                onlinePct.Add(dataCollectionDivision, pct);
                targetTotalResultsNum.Add(dataCollectionDivision, targetTotalResults);
                refTotalResultsNum.Add(dataCollectionDivision, refTotalResults);
            }

            StringArrayMetric _dataCollectionDivisions = new StringArrayMetric(MetricName.dataCollectionDivision,
                    onlinePct.Keys.ToArray(),
                     "string");
            log.DebugFormat("Data Collection Division : {0}{1}", string.Join(",", _dataCollectionDivisions.Value), _dataCollectionDivisions.Uom);
            metrics.Add(_dataCollectionDivisions);

            LongArrayMetric _targetTotalResults = new LongArrayMetric(MetricName.totalResults,
                    targetTotalResultsNum.Values.ToArray(),
                     "#");
            log.DebugFormat("Target Total Results : {0}{1}", string.Join(",", _targetTotalResults.Value), _targetTotalResults.Uom);
            metrics.Add(_targetTotalResults);

            LongArrayMetric _refTotalResults = new LongArrayMetric(MetricName.totalReferenceResults,
                    refTotalResultsNum.Values.ToArray(),
                     "#");
            log.DebugFormat("Reference Total Results : {0}{1}", string.Join(",", _refTotalResults.Value), _refTotalResults.Uom);
            metrics.Add(_refTotalResults);

            DoubleArrayMetric _dataCoverage = new DoubleArrayMetric(MetricName.dataOfferConsistency,
                    onlinePct.Values.ToArray(),
                     "%");
            log.DebugFormat("Data Offer COnsistency : {0}{1}", string.Join(",", _dataCoverage.Value), _dataCoverage.Uom);
            metrics.Add(_dataCoverage);

            var _tcr = new TestCaseResult(testCase503.Id, metrics, testCase503.StartTime, testCase503.EndTime);
            _tcr.ClassName = testCase503.GetType().ToString();
            return _tcr;
        }

        internal static TestCaseResult GenerateTestCase601Result(TestCase601 testCase601)
        {
            List<IMetric> metrics = new List<IMetric>();

            if (testCase601.Results.Count() == 0)
            {
                DoubleMetric _errorRate2 = new DoubleMetric(MetricName.errorRate,
                100,
                 "%");
                log.DebugFormat("Error Rate : {0}{1}", _errorRate2.Value, _errorRate2.Uom);
                metrics.Add(_errorRate2);
                var _tcr2 = new TestCaseResult(testCase601.Id, metrics, testCase601.StartTime, testCase601.EndTime);
                _tcr2.ClassName = testCase601.GetType().ToString();
                return _tcr2;
            }

            Dictionary<string, double> totalResults = new Dictionary<string, double>();
            Dictionary<string, double> wrongResultsCounts = new Dictionary<string, double>();
            Dictionary<string, double> validatedResultsCounts = new Dictionary<string, double>();
            Dictionary<string, double> readResultsCounts = new Dictionary<string, double>();
            Dictionary<string, long> opsAvgLatencies = new Dictionary<string, long>();
            Dictionary<string, long> opsMaxLatencies = new Dictionary<string, long>();
            // Dictionary<string, ExceptionMetric> exceptions = new Dictionary<string, ExceptionMetric>();

            double errors = 0;
            foreach (var testUnit in testCase601.Results)
            {
                string dataCollectionDivision = testUnit.Metrics.Where(m => m.Name == MetricName.dataCollectionDivision).Cast<StringMetric>().Select(m => m.Value).FirstOrDefault();

                long totalResult = testUnit.Metrics.Where(m => m.Name == MetricName.maxTotalResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                long readResultsCount = testUnit.Metrics.Where(m => m.Name == MetricName.totalReadResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                long validatedResultsCount = testUnit.Metrics.Where(m => m.Name == MetricName.totalValidatedResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                long wrongResultsCount = testUnit.Metrics.Where(m => m.Name == MetricName.wrongResultsCount).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                ExceptionMetric exception = (ExceptionMetric)testUnit.Metrics.Where(m => m.Name == MetricName.exception).FirstOrDefault();

                if (exception != null)
                {
                    errors++;
                }

                totalResults.Add(dataCollectionDivision, totalResult);
                validatedResultsCounts.Add(dataCollectionDivision, validatedResultsCount);
                readResultsCounts.Add(dataCollectionDivision, readResultsCount);
                wrongResultsCounts.Add(dataCollectionDivision, wrongResultsCount);
                // exceptions.Add(dataCollectionDivision, exception);

                try
                {
                    opsAvgLatencies.Add(dataCollectionDivision, ((LongMetric)testUnit.Metrics.FirstOrDefault(m => m.Name == MetricName.avgDataOperationalLatency)).Value);
                    opsMaxLatencies.Add(dataCollectionDivision, ((LongMetric)testUnit.Metrics.FirstOrDefault(m => m.Name == MetricName.maxDataOperationalLatency)).Value);
                }
                catch
                {
                    opsAvgLatencies.Add(dataCollectionDivision, -1);
                    opsMaxLatencies.Add(dataCollectionDivision, -1);
                }
            }

            DoubleMetric _errorRate = new DoubleMetric(MetricName.errorRate,
                    (errors / testCase601.Results.Count()) * 100,
                     "%");
            log.DebugFormat("Error Rate : {0}{1}", _errorRate.Value, _errorRate.Uom);
            metrics.Add(_errorRate);

            StringArrayMetric _dataCollectionDivisions = new StringArrayMetric(MetricName.dataCollectionDivision,
                    totalResults.Keys.ToArray(),
                     "string");
            log.DebugFormat("Data Collection Division : {0}", string.Join(",", _dataCollectionDivisions.Value));
            metrics.Add(_dataCollectionDivisions);

            // StringArrayMetric _exceptions = new StringArrayMetric(MetricName.exception,
            //         exceptions.Values.Select(e => e == null ? null : e.Exception.Message).ToArray(),
            //          "string");
            // log.DebugFormat("Exceptions : {0}", string.Join(",", _exceptions.Value));
            // metrics.Add(_exceptions);

            DoubleArrayMetric _coverages = new DoubleArrayMetric(MetricName.maxTotalResults,
                   totalResults.Values.ToArray(),
                    "#");
            log.DebugFormat("Total Results : {0}", string.Join(",", _coverages.Value));
            metrics.Add(_coverages);

            DoubleArrayMetric _totalRead = new DoubleArrayMetric(MetricName.totalReadResults,
                    readResultsCounts.Values.ToArray(),
                     "#");
            log.DebugFormat("Read : {0}", string.Join(",", _totalRead.Value));
            metrics.Add(_totalRead);

            DoubleArrayMetric _wrong = new DoubleArrayMetric(MetricName.totalWrongResults,
                    wrongResultsCounts.Values.ToArray(),
                     "#");
            log.DebugFormat("Wrong : {0}", string.Join(",", _wrong.Value));
            metrics.Add(_wrong);

            DoubleArrayMetric _validated = new DoubleArrayMetric(MetricName.totalValidatedResults,
                    validatedResultsCounts.Values.ToArray(),
                     "#");
            log.DebugFormat("Validated : {0}", string.Join(",", _validated.Value));
            metrics.Add(_validated);

            LongArrayMetric _opsAvgLatencies = new LongArrayMetric(MetricName.avgDataOperationalLatency,
                    opsAvgLatencies.Values.ToArray(),
                     "sec");
            log.DebugFormat("Operational Average Latencies : {0}", string.Join(",", Array.ConvertAll(_opsAvgLatencies.Value, t => TimeSpan.FromSeconds(t).ToString())));
            metrics.Add(_opsAvgLatencies);

            LongArrayMetric _opsMaxLatencies = new LongArrayMetric(MetricName.maxDataOperationalLatency,
                    opsMaxLatencies.Values.ToArray(),
                     "sec");
            log.DebugFormat("Operational Max Latencies : {0}", string.Join(",", Array.ConvertAll(_opsMaxLatencies.Value, t => TimeSpan.FromSeconds(t).ToString())));
            metrics.Add(_opsMaxLatencies);

            var _tcr = new TestCaseResult(testCase601.Id, metrics, testCase601.StartTime, testCase601.EndTime);
            _tcr.ClassName = testCase601.GetType().ToString();
            return _tcr;
        }

        internal static TestCaseResult GenerateTestCase602Result(TestCase602 testCase602)
        {
            List<IMetric> metrics = new List<IMetric>();

            if (testCase602.Results.Count() == 0)
            {
                DoubleMetric _errorRate2 = new DoubleMetric(MetricName.errorRate,
                100,
                 "%");
                log.DebugFormat("Error Rate : {0}{1}", _errorRate2.Value, _errorRate2.Uom);
                metrics.Add(_errorRate2);
                var _tcr2 = new TestCaseResult(testCase602.Id, metrics, testCase602.StartTime, testCase602.EndTime);
                _tcr2.ClassName = testCase602.GetType().ToString();
                return _tcr2;
            }

            Dictionary<string, double> totalResults = new Dictionary<string, double>();
            Dictionary<string, double> wrongResultsCounts = new Dictionary<string, double>();
            Dictionary<string, double> validatedResultsCounts = new Dictionary<string, double>();
            Dictionary<string, double> readResultsCounts = new Dictionary<string, double>();
            Dictionary<string, long> avaAvgLatencies = new Dictionary<string, long>();
            Dictionary<string, long> avaMaxLatencies = new Dictionary<string, long>();
            Dictionary<string, ExceptionMetric> exceptions = new Dictionary<string, ExceptionMetric>();

            long errors = 0;
            foreach (var testUnit in testCase602.Results)
            {
                string dataCollectionDivision = testUnit.Metrics.Where(m => m.Name == MetricName.dataCollectionDivision).Cast<StringMetric>().Select(m => m.Value).FirstOrDefault();
                long totalResult = testUnit.Metrics.Where(m => m.Name == MetricName.maxTotalResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                long readResultsCount = testUnit.Metrics.Where(m => m.Name == MetricName.totalReadResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                long validatedResultsCount = testUnit.Metrics.Where(m => m.Name == MetricName.totalValidatedResults).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                long wrongResultsCount = testUnit.Metrics.Where(m => m.Name == MetricName.wrongResultsCount).Cast<LongMetric>().Select(m => m.Value).FirstOrDefault();
                ExceptionMetric exception = (ExceptionMetric)testUnit.Metrics.Where(m => m.Name == MetricName.exception).FirstOrDefault();

                if (exception != null)
                {
                    errors++;
                }

                if (totalResults.ContainsKey(dataCollectionDivision))
                {
                    log.WarnFormat("Data collection with key '{0} is duplicated, please check the config");
                    continue;
                }
                totalResults.Add(dataCollectionDivision, totalResult);
                validatedResultsCounts.Add(dataCollectionDivision, validatedResultsCount);
                readResultsCounts.Add(dataCollectionDivision, readResultsCount);
                wrongResultsCounts.Add(dataCollectionDivision, wrongResultsCount);
                exceptions.Add(dataCollectionDivision, exception);

                var avaAvgLatency = testUnit.Metrics.FirstOrDefault(m => m.Name == MetricName.avgDataAvailabilityLatency);
                var avaMaxLatency = testUnit.Metrics.FirstOrDefault(m => m.Name == MetricName.maxDataAvailabilityLatency);

                if (avaAvgLatency != null)
                    avaAvgLatencies.Add(dataCollectionDivision, ((LongMetric)avaAvgLatency).Value);
                else
                    avaAvgLatencies.Add(dataCollectionDivision, -1);
                if (avaMaxLatency != null)
                    avaMaxLatencies.Add(dataCollectionDivision, ((LongMetric)avaMaxLatency).Value);
                else
                    avaMaxLatencies.Add(dataCollectionDivision, -1);

            }

            DoubleMetric _errorRate = new DoubleMetric(MetricName.errorRate,
                   (errors / testCase602.Results.Count()) * 100,
                    "%");
            log.DebugFormat("Error Rate : {0}{1}", _errorRate.Value, _errorRate.Uom);
            metrics.Add(_errorRate);

            StringArrayMetric _dataCollectionDivisions = new StringArrayMetric(MetricName.dataCollectionDivision,
                    totalResults.Keys.ToArray(),
                     "string");
            log.DebugFormat("Data Collection Division : {0}", string.Join(",", _dataCollectionDivisions.Value));
            metrics.Add(_dataCollectionDivisions);

            StringArrayMetric _exceptions = new StringArrayMetric(MetricName.exception,
                    exceptions.Values.Select(e => e == null ? null : e.Exception.Message).ToArray(),
                     "string");
            log.DebugFormat("Exceptions : {0}", string.Join(",", _exceptions.Value));
            metrics.Add(_exceptions);

            DoubleArrayMetric _coverages = new DoubleArrayMetric(MetricName.maxTotalResults,
                    totalResults.Values.ToArray(),
                     "#");
            log.DebugFormat("Total Results : {0}", string.Join(",", _coverages.Value));
            metrics.Add(_coverages);

            DoubleArrayMetric _totalRead = new DoubleArrayMetric(MetricName.totalReadResults,
                    readResultsCounts.Values.ToArray(),
                     "#");
            log.DebugFormat("Read : {0}", string.Join(",", _totalRead.Value));
            metrics.Add(_totalRead);

            DoubleArrayMetric _wrong = new DoubleArrayMetric(MetricName.totalWrongResults,
                    wrongResultsCounts.Values.ToArray(),
                     "#");
            log.DebugFormat("Wrong : {0}", string.Join(",", _wrong.Value));
            metrics.Add(_wrong);

            DoubleArrayMetric _validated = new DoubleArrayMetric(MetricName.totalValidatedResults,
                    validatedResultsCounts.Values.ToArray(),
                     "#");
            log.DebugFormat("Validated : {0}", string.Join(",", _validated.Value));
            metrics.Add(_validated);

            LongArrayMetric _avaAvgLatencies = new LongArrayMetric(MetricName.avgDataAvailabilityLatency,
                    avaAvgLatencies.Values.ToArray(),
                     "sec");
            log.DebugFormat("Availability Average Latencies : {0}", string.Join(",", Array.ConvertAll(_avaAvgLatencies.Value, t => TimeSpan.FromSeconds(t).ToString())));
            metrics.Add(_avaAvgLatencies);

            LongArrayMetric _avaMaxLatencies = new LongArrayMetric(MetricName.maxDataAvailabilityLatency,
                    avaMaxLatencies.Values.ToArray(),
                     "sec");
            log.DebugFormat("Availability Max Latencies : {0}", string.Join(",", Array.ConvertAll(_avaMaxLatencies.Value, t => TimeSpan.FromSeconds(t).ToString())));
            metrics.Add(_avaMaxLatencies);



            var _tcr = new TestCaseResult(testCase602.Id, metrics, testCase602.StartTime, testCase602.EndTime);
            _tcr.ClassName = testCase602.GetType().ToString();
            return _tcr;
        }
    }
}
