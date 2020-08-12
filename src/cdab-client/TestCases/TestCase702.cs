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
    internal class TestCase702 : TestCase301
    {

        private string storageName;
        private readonly List<string> uploadedFiles;

        public string TestFile { get; set; }

        public TestCase702(ILog log, TargetSiteWrapper target, string storageName, List<string> uploadedFiles) : base(log, target, null)
        {
            this.Id = "TC702";
            this.Title = "Single remote download from cloud storage";
            this.storageName = storageName;
            this.uploadedFiles = uploadedFiles;
        }

        public override void PrepareTest()
        {
            log.DebugFormat("Prepare {0}", Id);

            if (uploadedFiles.Count == 0)
            {
                throw new InvalidOperationException("No previous uploaded files found...");
            }

            var storageClient = target.Wrapper.CreateStorageClient();
            try
            {
                storageClient.Prepare();

                foreach (var item in uploadedFiles)
                {
                    log.DebugFormat("Filo to download: {0}", item);
                    if (item == null)
                        continue;
                    CreateDownloadTransferAndEnqueue(storageClient, item);
                }
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

            }
        }

        private void CreateDownloadTransferAndEnqueue(IStorageClient storageClient, string item)
        {
            var transfer = storageClient.CreateDownloadRequest(storageName, item);
            downloadRequests.Enqueue(SingleEnclosureAccess.Create(transfer, transfer.Method, null, 0));
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
                }, tasks.Result.Count());
                testCaseResults.SearchFiltersDefinition = results.Where(tcr => tcr != null).Select(tcr => tcr.FiltersDefinition).ToList();

                var storageClient = target.Wrapper.CreateStorageClient();
                storageClient.Prepare();
                log.DebugFormat("Deleting storage {0}...", storageName);
                storageClient.DeleteStorage(storageName, true);
                log.DebugFormat("OK");

                return testCaseResults;
            }
            catch (AggregateException e)
            {
                log.WarnFormat("Test Case Execution Error : {0}", e.InnerException.Message);
                throw e;
            }

        }


    }
}