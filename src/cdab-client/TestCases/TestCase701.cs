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

If you modify this file, or any covered work, by linking or combining it with Terradue.OpenSearch.SciHub 
(or a modified version of that library), containing parts covered by the terms of CC BY-NC-ND 3.0, 
the licensors of this Program grant you additional permission to convey or distribute the resulting work.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using cdabtesttools.Config;
using cdabtesttools.Data;
using cdabtesttools.Measurement;
using cdabtesttools.Target;
using log4net;
using Terradue.Metadata.EarthObservation.OpenSearch.Extensions;
using Terradue.OpenSearch;
using Terradue.OpenSearch.DataHub;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Result;
using Terradue.ServiceModel.Ogc.Eop21;
using Terradue.ServiceModel.Syndication;

namespace cdabtesttools.TestCases
{
    internal class TestCase701 : TestCase301
    {
        protected ConcurrentQueue<ITransferRequest> uploadRequests;

        private string storageName;
        private readonly List<string> uploadedFiles;
        private readonly List<string> failedUploads;

        public TestCase701(ILog log, TargetSiteWrapper target, int load_factor, string storageName, List<string> uploadedFiles) :
            base(log, target, null)
        {
            this.Id = "TC701";
            this.Title = "Single remote upload to cloud storage";
            this.load_factor = load_factor;
            uploadRequests = new ConcurrentQueue<ITransferRequest>();
            this.storageName = storageName;
            this.uploadedFiles = uploadedFiles;
            this.failedUploads = new List<string>();
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);


            var storageClient = target.Wrapper.CreateStorageClient();

            try
            {
                storageClient.Prepare();
                log.DebugFormat("Creating storage {0}...", storageName);
                storageClient.CreateStorage(storageName);
                log.DebugFormat("OK");
            }
            catch (Exception e)
            {
                log.ErrorFormat("Error creating the storage '{0}': {1}", storageName, e.Message);
                if (e is WebException)
                {
                    var we = e as WebException;
                    using (var response = new StreamReader(we.Response.GetResponseStream()))
                    {
                        log.Debug(response.ReadToEnd());
                    }
                }
                throw e;
            }

            for (int i = 0 ; i < 10 && uploadRequests.Count() < load_factor ; i++)
            {
                try
                {
                    CreateUploadAndEnqueue(storageClient, storageName);
                }
                catch (Exception e)
                {
                    log.ErrorFormat("Error creating the upload request: {0}", e.Message);
                    throw e;
                }
            }
        }

        private void CreateUploadAndEnqueue(IStorageClient storageClient, string storageName)
        {
            string fileName = GenerateName(10) + ".dat";
            
            int minSize = 0;
            int maxSize = 0;
            if (target.TargetSiteConfig.Storage != null)
            {
                minSize = target.TargetSiteConfig.Storage.MinUploadSize;
                maxSize = target.TargetSiteConfig.Storage.MaxUploadSize;
            }
            if (minSize <= 0 || maxSize <= 0 || minSize > maxSize)
            {
                minSize = 5;
                maxSize = 25;
                log.InfoFormat("Missing or invalid upload file size range, using default {0}MB - {1}MB", minSize, maxSize);
            }
            Random rng = new Random();
            ulong totalSize = Convert.ToUInt64(rng.Next(minSize, maxSize) * 1024 * 1024);
            log.InfoFormat("Upload file size: {0} (range is from {1}MB to {2}MB)", BytesToString(Convert.ToInt64(totalSize)), minSize, maxSize);
            if (Configuration.Current.Global.TestMode) totalSize = 20 * 1024 * 1024;
            var uploadRequest = storageClient.CreateUploadRequest(storageName, fileName, totalSize);

            if (uploadRequest is HttpTransferRequest) {
                var httpUploadRequest = uploadRequest as HttpTransferRequest;
                httpUploadRequest.HttpWebRequest.AllowWriteStreamBuffering = false;
            }

            uploadRequests.Enqueue(uploadRequest);
            uploadedFiles.Add(fileName);
        }

        public static string GenerateName(int len)
        {
            Random r = new Random();
            string[] consonants = { "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "sh", "zh", "t", "v", "w", "x" };
            string[] vowels = { "a", "e", "i", "o", "u", "ae", "y" };
            string Name = "";
            Name += consonants[r.Next(consonants.Length)].ToUpper();
            Name += vowels[r.Next(vowels.Length)];
            int b = 2; //b tells how many times a new letter has been added. It's 2 right now because the first two letters are already in the name.
            while (b < len)
            {
                Name += consonants[r.Next(consonants.Length)];
                b++;
                Name += vowels[r.Next(vowels.Length)];
                b++;
            }

            return Name;
        }



        public override TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks)
        {

            // Remove failed uploads from list (to prevent download attempts in the next test case)
            string[] uploadedFilesCopy = uploadedFiles.ToArray();
            foreach (string uploadedFile in uploadedFilesCopy) {
                foreach (string failedUpload in failedUploads)
                    if (failedUpload.Contains(uploadedFile))
                        uploadedFiles.Remove(uploadedFile);
            }

            List<IMetric> _testCaseMetric = new List<IMetric>();

            try
            {
                results = tasks.Result.ToList();
                var testCaseResults = MeasurementsAnalyzer.GenerateTestCaseResult(this, new MetricName[]{
                    MetricName.avgResponseTime,
                    MetricName.peakResponseTime,
                    MetricName.errorRate,
                    MetricName.maxSize,
                    MetricName.totalSize,
                    MetricName.resultsErrorRate,
                    MetricName.throughput,
                }, tasks.Result.Count());
                testCaseResults.SearchFiltersDefinition = results.Where(tcr => tcr != null).Select(tcr => tcr.FiltersDefinition).ToList();
                return testCaseResults;
            }
            catch (AggregateException e)
            {
                log.WarnFormat("Test Case Execution Error : {0}", e.InnerException.Message);
                throw e;
            }
        }

        public override IEnumerable<TestUnitResult> RunTestUnits(TaskFactory taskFactory, Task prepTask)
        {
            StartTime = DateTimeOffset.UtcNow;
            List<Task<TestUnitResult>> _testUnits = new List<Task<TestUnitResult>>();
            Task[] previousTask = Array.ConvertAll(new int[target.TargetSiteConfig.MaxUploadThread], i => prepTask);

            if (!uploadRequests.IsEmpty)
                log.InfoFormat("Starting concurrent upload(s) [{0}] of {1} item(s)", target.TargetSiteConfig.MaxUploadThread, load_factor);

            for (int i = 0; i < load_factor && !uploadRequests.IsEmpty;)
            {
                for (int j = 0; j < load_factor && j < target.TargetSiteConfig.MaxUploadThread && !uploadRequests.IsEmpty; j++, i++)
                {
                    Task<TestUnitResult> _testUnit = previousTask[j].ContinueWith<ITransferRequest>((task) =>
                    {
                        prepTask.Wait();
                        ITransferRequest request;
                        uploadRequests.TryDequeue(out request);
                        return request;
                    }).ContinueWith((request) => Upload(request.Result));
                    _testUnits.Add(_testUnit);
                    previousTask[j] = _testUnit;
                }
            }
            Task.WaitAll(_testUnits.ToArray());
            EndTime = DateTimeOffset.UtcNow;
            return _testUnits.Select(t => t.Result);
        }

        protected TestUnitResult Upload(ITransferRequest transferRequest)
        {
            if (transferRequest == null)
                return null;

            List<IMetric> metrics = new List<IMetric>();

            metrics.Add(new StringMetric(MetricName.url, transferRequest.RequestUri.ToString(), ""));

            int taskId = Task.CurrentId.Value;

            DateTimeOffset timeStart = DateTimeOffset.UtcNow;
            metrics.Add(new DateTimeMetric(MetricName.startTime, timeStart, "dateTime"));

            List<ItemAccessMethod> methods = new List<ItemAccessMethod>() { transferRequest.Method };

            if (methods.Count() == 0)
            {
                log.WarnFormat("No upload methods found. Skipping upload.");
                var tcr1 = new TestUnitResult(metrics, TestUnitResultStatus.NotStarted);
                tcr1.State = transferRequest;
                metrics.Add(new DateTimeMetric(MetricName.endTime, DateTimeOffset.UtcNow, "dateTime"));
                return tcr1;
            }

            var method = methods.First();

            long respTime = 0;
            Random rng = new Random();
            long totalSize = Convert.ToInt64(transferRequest.ContentLength);
            log.DebugFormat("Upload {1} to {0} ({2}) ...", transferRequest.RequestUri, BytesToString(Convert.ToInt64(totalSize)), transferRequest.Method);

            Stopwatch stopWatchUploadElaspedTime = new Stopwatch();

            metrics.Add(new LongMetric(MetricName.beginGetResponseTime, DateTimeOffset.UtcNow.Ticks, "ticks"));
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var uploadStreamTask = transferRequest.GetRequestStreamAsync();
            uploadStreamTask.ContinueWith(streamTask =>
            {
                respTime = sw.ElapsedMilliseconds;
                Stream stream = streamTask.Result;
                byte[] buf = new byte[4096];
                double progessPct = 0;
                long totalByteCounter = 0;
                long intermediateCounter = 0;
                stopWatchUploadElaspedTime.Start();
                while (totalByteCounter < totalSize)
                {
                    rng.NextBytes(buf);
                    int length = buf.Length;
                    if ( totalSize < totalByteCounter + length )
                        length = Convert.ToInt32(totalSize - totalByteCounter);
                    stream.Write(buf, 0, buf.Length);
                    totalByteCounter += buf.Length;
                    intermediateCounter += buf.Length;
                    double totalPct = ((double)totalByteCounter / totalSize) * 100;
                    if ((Math.Abs(progessPct - totalPct) >= 1) || intermediateCounter >= 10485760)
                    {
                        progessPct = totalPct;
                        double bytespersec = (double)totalByteCounter / ((double)stopWatchUploadElaspedTime.ElapsedMilliseconds / 1000);
                        log.DebugFormat("[{2}] Uploaded {0}% [{1}/s]", totalPct.ToString("F1"), BytesToString((long)bytespersec), taskId);
                                           
                        intermediateCounter = 0;
                    }

                    if (Configuration.Current.Global.TestMode && totalByteCounter >= 10485760 * 2)
                    {
                        // Test Breaker at 20MB )
                        log.Warn("!!!!!TEST DOWNLOAD BREAKER!!!!");
                        break;
                    }
                }
                stream.Close();
                stopWatchUploadElaspedTime.Stop();
            });

            try
            {
                uploadStreamTask.Wait();
            }
            catch (Exception e)
            {
                Exception ie = e;
                while (ie.InnerException != null)
                {
                    ie = ie.InnerException;
                }
                if (ie is WebException)
                {
                    WebException we = ie as WebException;
                    log.DebugFormat("[{0}] Error uploading {2}. {4} Error: {1}[{3}]", Task.CurrentId, we.Message, transferRequest.RequestUri, we.Status.ToString(), method);
                    if (we.Response is HttpWebResponse)
                        metrics.Add(new StringMetric(MetricName.httpStatusCode, string.Format("{0}:{1}", (int)((HttpWebResponse)we.Response).StatusCode, ((HttpWebResponse)we.Response).StatusDescription), ""));

                    metrics.Add(new ExceptionMetric(we));
                }
                else
                {
                    log.DebugFormat("[{0}] Error during upload {2}. Exception: {1}", Task.CurrentId, ie.Message, transferRequest.RequestUri);
                    log.Debug(ie.StackTrace);
                    metrics.Add(new ExceptionMetric(ie));
                }

                failedUploads.Add(transferRequest.RequestUri.AbsoluteUri);
            }

            var uploadTask = transferRequest.GetResponseAsync().ContinueWith(resp =>
            {
                sw.Stop();

                using (ITransferResponse response = resp.Result)
                {

                    log.InfoFormat("[{1}] > {3} Status Code {0} ({2}ms)", response.StatusCode, taskId, respTime, method);
                    metrics.Add(new StringMetric(MetricName.httpStatusCode, string.Format("{0}:{1}", (int)response.StatusCode, response.StatusDescription), ""));
                    if (!(response.StatusCode == TransferStatusCode.OK || response.StatusCode == TransferStatusCode.Accepted || response.StatusCode == TransferStatusCode.Created))
                    {
                        log.DebugFormat("[{0}] < Not OK. Exception: {1}", taskId, response.StatusDescription);
                        metrics.Add(new ExceptionMetric(new Exception(
                            string.Format("[{0}] < Not OK. Exception: {1}", taskId, response.StatusDescription))));
                    }
                    else
                    {
                        log.InfoFormat("[{0}] < OK. Content Type {1}", taskId, response.ContentType);
                    }
                }

            });

            try
            {
                uploadTask.Wait();
            }
            catch (Exception e)
            {
                Exception ie = e;
                while (ie.InnerException != null)
                {
                    ie = ie.InnerException;
                }
                if (ie is WebException)
                {
                    WebException we = ie as WebException;
                    log.DebugFormat("[{0}] Error uploading {2}. {4} Error: {1}[{3}]", Task.CurrentId, we.Message, transferRequest.RequestUri, we.Status.ToString(), method);
                    if (we.Response is HttpWebResponse)
                        metrics.Add(new StringMetric(MetricName.httpStatusCode, string.Format("{0}:{1}", (int)((HttpWebResponse)we.Response).StatusCode, ((HttpWebResponse)we.Response).StatusDescription), ""));

                    metrics.Add(new ExceptionMetric(we));
                }
                else
                {
                    log.DebugFormat("[{0}] Error during upload {2}. Exception: {1}", Task.CurrentId, ie.Message, transferRequest.RequestUri);
                    log.Debug(ie.StackTrace);
                    metrics.Add(new ExceptionMetric(ie));
                }

                failedUploads.Add(transferRequest.RequestUri.AbsoluteUri);
            }

            DateTimeOffset timeStop = DateTimeOffset.UtcNow;
            metrics.Add(new DateTimeMetric(MetricName.endTime, timeStop, "dateTime"));
            metrics.Add(new LongMetric(MetricName.endGetResponseTime, DateTime.UtcNow.Ticks, "ticks"));
            metrics.Add(new LongMetric(MetricName.responseTime, respTime, "ms"));
            metrics.Add(new LongMetric(MetricName.size, totalSize, "bytes"));
            metrics.Add(new LongMetric(MetricName.downloadElapsedTime, stopWatchUploadElaspedTime.ElapsedMilliseconds, "ms"));
            metrics.Add(new LongMetric(MetricName.maxTotalResults, 1, "#"));
            metrics.Add(new LongMetric(MetricName.totalReadResults, 1, "#"));
            metrics.Add(new LongMetric(MetricName.wrongResultsCount, 0, "#"));
            var tcr = new TestUnitResult(metrics);
            tcr.State = transferRequest;
            return tcr;
        }
    }

}
