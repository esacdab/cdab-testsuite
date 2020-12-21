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
    internal class TestCase702 : TestCase301
    {
        private string storageName;
        private readonly List<string> uploadedFiles;

        public string TestFile { get; set; }

        public TestCase702(ILog log, TargetSiteWrapper target, int load_factor, string storageName, List<string> uploadedFiles) : base(log, target, load_factor, null)
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
                    log.DebugFormat("File to download: {0}", item);
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
                foreach (var item in uploadedFiles) {
                    log.DebugFormat("Deleting file {0}...", item);
                    storageClient.DeleteFile(storageName, item);
                }
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
