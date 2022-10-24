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
using Terradue.OpenSearch.DataHub;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Result;
using Terradue.OpenSearch.DataHub.Dias;
using Terradue.ServiceModel.Ogc.Eop21;
using Terradue.ServiceModel.Syndication;

namespace cdabtesttools.TestCases
{
    internal class TestCase301 : TestCase
    {
        protected readonly TargetSiteWrapper target;
        protected int load_factor;
        protected readonly List<IOpenSearchResultItem> foundItems;
        protected readonly ILog log;
        private ServicePoint sp;
        protected ConcurrentQueue<IAssetAccess> downloadRequests;
        protected OpenSearchEngine ose = new OpenSearchEngine();
        protected bool preauth = true;

        protected int max_try_for_finding_download = 3;

        public TestCase301(ILog log, TargetSiteWrapper target, List<IOpenSearchResultItem> foundItems) :
            this(log, target, 1, foundItems) {}

        public TestCase301(ILog log, TargetSiteWrapper target, int load_factor, List<IOpenSearchResultItem> foundItems) :
            base("TC301", "Single Remote Download")
        {
            this.log = log;
            this.load_factor = load_factor;
            this.foundItems = foundItems;
            this.target = target;
            this.sp = ServicePointManager.FindServicePoint(target.Wrapper.Settings.ServiceUrl);
            downloadRequests = new ConcurrentQueue<IAssetAccess>();
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);
            List<IOpenSearchResultItem> testFoundItems = foundItems;
            if (testFoundItems.Count > 0)
                log.DebugFormat("Choose randomly the item to download from previous tests results...");
            else
            {
                log.DebugFormat("No previous tests results...");
                testFoundItems = SearchForMoreItemsToDownload();
            }

            while (downloadRequests.Count() < load_factor && max_try_for_finding_download > 0)
            {
                foreach (var item in testFoundItems)
                {
                    if (item == null)
                        continue;
                    if (downloadRequests.Count() >= load_factor)
                        break;
                    CreateDownloadRequestAndEnqueue(item);
                }
                if (downloadRequests.Count() < load_factor)
                {
                    testFoundItems = SearchForMoreItemsToDownload();
                    if (testFoundItems == null) break;
                }
                max_try_for_finding_download--;
            }
        }

        protected virtual List<IOpenSearchResultItem> SearchForMoreItemsToDownload()
        {
            log.WarnFormat("Not enough items found ready for downloading ({0}). Requesting more items.", downloadRequests.Count());
            return FindItemsToDownload(100);
        }


        protected virtual void CreateDownloadRequestAndEnqueue(IOpenSearchResultItem item)
        {
            log.DebugFormat("Creating download request for {0}...", item.Identifier);
            try
            {
                IAssetAccess enclosureAccess = target.Wrapper.GetEnclosureAccess(item);
                if (enclosureAccess != null)
                {
                    if (Convert.ToInt64(enclosureAccess.TotalSize) > target.TargetSiteConfig.MaxDownloadSize)
                        throw new Exception(String.Format("Product file too large ({0} > {1}) for {2}", enclosureAccess.TotalSize, target.TargetSiteConfig.MaxDownloadSize, item.Identifier));
                    downloadRequests.Enqueue(enclosureAccess);
                    log.DebugFormat("OK --> {0}", enclosureAccess.Uri);
                }
            }
            catch (Exception e)
            {
                log.WarnFormat("NOT OK: {0}", e.Message);
                log.Debug(e.StackTrace);
            }
        }

        protected virtual List<IOpenSearchResultItem> FindItemsToDownload(int count = 20)
        {
            List<IOpenSearchResultItem> forcedFoundItems = new List<IOpenSearchResultItem>();
            TestCase201 tc = new TestCase201(log, target, 1, Mission.GenerateExistingDataDictionary(target), out forcedFoundItems);
            var filters = new FiltersDefinition("all");
            filters.AddFilter("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", "Sentinel-1", "Sentinel-1", null, null);
            filters.AddFilter("productType", "{http://a9.com/-/opensearch/extensions/eo/1.0/}productType", "GRD", "GRD", null, null);
            filters.AddFilter("archiveStatus", "{http://a9.com/-/opensearch/extensions/eo/1.0/}statusSubType", "online", "Online", null, null);
            filters.AddFilter("productFormat", "{http://a9.com/-/opensearch/extensions/eo/1.0/}productFormat", "zip", "ZIP", null, null);
            filters.AddFilter("count", "{http://a9.com/-/spec/opensearch/1.1/}count", count > 50 ? count.ToString() : "50", "", null, null);
            tc.MakeQuery(target.CreateOpenSearchableEntity(), filters);
            return forcedFoundItems;
        }

        public override TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks)
        {
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
                    MetricName.dataAccess,
                    MetricName.dataCollectionDivision
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
            Task[] previousTask = Array.ConvertAll(new int[target.TargetSiteConfig.MaxDownloadThread], i => prepTask);

            if (!downloadRequests.IsEmpty)
                log.InfoFormat("Starting concurrent download(s) [{0}] of {1} item(s)", target.TargetSiteConfig.MaxDownloadThread, load_factor);

            for (int i = 0; i < load_factor && !downloadRequests.IsEmpty;)
            {
                for (int j = 0; j < load_factor && j < target.TargetSiteConfig.MaxDownloadThread && !downloadRequests.IsEmpty; j++, i++)
                {
                    Task<TestUnitResult> _testUnit = previousTask[j].ContinueWith<IAssetAccess>((task) =>
                    {
                        prepTask.Wait();

                        IAssetAccess enclosureAccess = null;
                        if (downloadRequests.TryDequeue(out enclosureAccess))
                        {
                            log.DebugFormat("Dequeuing {0}", enclosureAccess.Uri);
                            return enclosureAccess;
                        }
                        return null;
                    }).ContinueWith((request) => Download(request.Result));
                    _testUnits.Add(_testUnit);
                    previousTask[j] = _testUnit;
                }
            }
            Task.WaitAll(_testUnits.ToArray());
            EndTime = DateTimeOffset.UtcNow;
            return _testUnits.Select(t => t.Result);
        }

        protected TestUnitResult Download(IAssetAccess enclosureAccess)
        {
            if (enclosureAccess == null)
                return null;

            List<IMetric> metrics = new List<IMetric>();

            metrics.Add(new StringMetric(MetricName.url, enclosureAccess.Uri.ToString(), ""));

            int i = 0;
            int taskId = Task.CurrentId.Value;

            DateTimeOffset timeStart = DateTimeOffset.UtcNow;
            metrics.Add(new DateTimeMetric(MetricName.startTime, timeStart, "dateTime"));

            long totalSize = Convert.ToInt64(enclosureAccess.TotalSize);
            log.DebugFormat("[{1}] Method #{3} {2}: GET {0} ({4}) ...", enclosureAccess.Uri, taskId, enclosureAccess.AccessMethod, i + 1, BytesToString(totalSize));
            var requests = enclosureAccess.GetDownloadRequests();
            long[] respTime = new long[requests.Count()];
            long totalByteCounter = 0;

            TestUnitResultStatus tcrStatus = TestUnitResultStatus.Complete;
            if ( requests == null || requests.Count() == 0){
                log.DebugFormat("[{1}] No download request for {0}. Skipping test unit.", enclosureAccess.Uri, taskId);
                tcrStatus = TestUnitResultStatus.NotStarted;
            }

            Stopwatch stopWatchDownloadElaspedTime = new Stopwatch();

            metrics.Add(new LongMetric(MetricName.beginGetResponseTime, DateTimeOffset.UtcNow.Ticks, "ticks"));
            int j = 0;

            foreach (var request in requests)
            {
                if (Configuration.Current.Global.TestMode && totalByteCounter >= 10485760 * 2)
                {
                    // Test Breaker at 20MB )
                    break;
                }
                j++;
                log.DebugFormat("[{1}] File #{0}/{3}: GET {2} ...", j, taskId, request.RequestUri, requests.Count());
                Stopwatch sw = new Stopwatch();
                sw.Start();

                var downloadTask = request.GetResponseAsync().ContinueWith(resp =>
                {
                    sw.Stop();

                    respTime[i] = sw.ElapsedMilliseconds;

                    using (ITransferResponse response = resp.Result)
                    {

                        log.InfoFormat("[{1}] > {3} Status Code {0} ({2}ms)", response.StatusCode, taskId, respTime[i], enclosureAccess.AccessMethod);
                        metrics.Add(new StringMetric(MetricName.httpStatusCode, string.Format("{0}:{1}", (int)response.StatusCode, response.StatusDescription), ""));
                        if (!(response.StatusCode == TransferStatusCode.OK || response.StatusCode == TransferStatusCode.Accepted))
                        {
                            log.DebugFormat("[{0}] < Not OK. Exception: {1}", taskId, response.StatusDescription);
                            metrics.Add(new ExceptionMetric(new Exception(
                                string.Format("[{0}] < Not OK. Exception: {1}", taskId, response.StatusDescription))));
                        }
                        else
                        {
                            using (var responseStream = response.GetResponseStream())
                            {
                                if (response.StatusCode == TransferStatusCode.OK && !response.ContentType.StartsWith("text/html"))
                                {
                                    string hsize = "unknown";
                                    if (response.ContentLength > 0)
                                    {
                                        hsize = BytesToString(response.ContentLength);
                                        if (totalSize == 0)
                                            totalSize = response.ContentLength;
                                    }
                                    log.InfoFormat("[{1}] < Content Type {2} Length {0}", hsize, taskId, response.ContentType);
                                    byte[] buf = new byte[4096];
                                    long fileSize = response.ContentLength;
                                    double progessPct = 0;
                                    long fileByteCounter = 0;
                                    long intermediateCounter = 0;
                                    stopWatchDownloadElaspedTime.Start();
                                    int nbByteRead = responseStream.Read(buf, 0, 4096);
                                    while (nbByteRead > 0)
                                    {
                                        totalByteCounter += nbByteRead;
                                        fileByteCounter += nbByteRead;
                                        intermediateCounter += nbByteRead;
                                        double totalPct = ((double)totalByteCounter / totalSize) * 100;
                                        double filePct = ((double)fileByteCounter / fileSize) * 100;
                                        string hpct = "";
                                        if ((fileSize > 0 && Math.Abs(progessPct - totalPct) > 1) || intermediateCounter >= 10485760)
                                        {
                                            if (fileSize > 0)
                                            {
                                                progessPct = totalPct;
                                                hpct = string.Format(" (file #{2}:{1}% / total:{0}%)", totalPct.ToString("F1"), filePct.ToString("F1"), j);
                                            }
                                            double bytespersec = (double)totalByteCounter / ((double)stopWatchDownloadElaspedTime.ElapsedMilliseconds / 1000);
                                            log.DebugFormat("[{3}] {0} downloaded{1} [{2}/s]", BytesToString(totalByteCounter), hpct, BytesToString((long)bytespersec), taskId);
                                            intermediateCounter = 0;
                                        }
                                        nbByteRead = responseStream.Read(buf, 0, 4096);

                                        if (Configuration.Current.Global.TestMode && totalByteCounter >= 10485760 * 2)
                                        {
                                            // Test Breaker at 20MB )
                                            log.Warn("!!!!!TEST DOWNLOAD BREAKER!!!!");
                                            break;
                                        }
                                    }
                                    stopWatchDownloadElaspedTime.Stop();
                                }
                            }
                        }
                    }

                });
                try
                {
                    downloadTask.Wait();
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
                        log.DebugFormat("[{0}] Error downloading {2}. {4} Error: {1}[{3}]", Task.CurrentId, we.Message, request.RequestUri, we.Status.ToString(), enclosureAccess.AccessMethod);
                        if (we.Response is HttpWebResponse)
                            metrics.Add(new StringMetric(MetricName.httpStatusCode, string.Format("{0}:{1}", (int)((HttpWebResponse)we.Response).StatusCode, ((HttpWebResponse)we.Response).StatusDescription), ""));

                        metrics.Add(new ExceptionMetric(we));
                    }
                    else
                    {
                        log.DebugFormat("[{0}] Error during download {2}. Exception: {1}", Task.CurrentId, ie.Message, enclosureAccess.Uri);
                        log.Debug(ie.StackTrace);
                        metrics.Add(new ExceptionMetric(ie));
                    }
                    break;
                }

                i++;
            }
            DateTimeOffset timeStop = DateTimeOffset.UtcNow;
            metrics.Add(new DateTimeMetric(MetricName.endTime, timeStop, "dateTime"));
            metrics.Add(new LongMetric(MetricName.endGetResponseTime, DateTime.UtcNow.Ticks, "ticks"));
            if (respTime.Count() > 0)
            {
                foreach (var r in respTime) metrics.Add(new LongMetric(MetricName.responseTime, r, "ms"));
            }
            else
            {
                metrics.Add(new LongMetric(MetricName.responseTime, 0, "ms"));
            }
            metrics.Add(new LongMetric(MetricName.size, totalByteCounter, "bytes"));
            metrics.Add(new LongMetric(MetricName.downloadElapsedTime, stopWatchDownloadElaspedTime.ElapsedMilliseconds, "ms"));
            metrics.Add(new LongMetric(MetricName.maxTotalResults, 1, "#"));
            metrics.Add(new LongMetric(MetricName.totalReadResults, 1, "#"));
            metrics.Add(new LongMetric(MetricName.wrongResultsCount, 0, "#"));
            var tcr = new TestUnitResult(metrics, tcrStatus);
            if (enclosureAccess.SourceItem != null)
            {
                FiltersDefinition fd = DataHelper.GenerateFiltersDefinitionFromItem("Download", enclosureAccess.SourceItem);
                metrics.Add(new StringMetric(MetricName.dataCollectionDivision, fd.Label, "string"));
                tcr.FiltersDefinition = fd;
                
                metrics.Add(new StringMetric(MetricName.dataAccess, GetDataAccessStr(enclosureAccess.AccessMethod, target), "string"));
            }
            tcr.State = enclosureAccess;
            return tcr;
        }

        protected static String BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount <= 0)
                return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }

        protected static string GetDataAccessStr(ItemAccessMethod accessMethod, TargetSiteWrapper target)
        {
            string dataAccessStr = null;
            switch (accessMethod)
            {
                case ItemAccessMethod.HttpDownload:
                    dataAccessStr = "http";
                    break;
                case ItemAccessMethod.FtpDownload:
                    dataAccessStr = "ftp";
                    break;
                case ItemAccessMethod.LocalFileSystem:
                    if (target.Wrapper is OndaDiasWrapper)
                    {
                        dataAccessStr = "ens";
                    }
                    else
                    {
                        dataAccessStr = "nfs";
                    }
                    break;
                case ItemAccessMethod.NetworkFileSystem:
                    if (target.Wrapper is OndaDiasWrapper)
                    {
                        dataAccessStr = "ens";
                    }
                    else
                    {
                        dataAccessStr = "nfs";
                    }
                    break;
                case ItemAccessMethod.S3Download:
                    dataAccessStr = "s3";
                    break;
                case ItemAccessMethod.Order:
                    dataAccessStr = "order";
                    break;
            }
            return dataAccessStr;
        }
    }
}
