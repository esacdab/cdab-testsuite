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
using cdabtesttools.Config;
using cdabtesttools.Data;
using cdabtesttools.Measurement;
using cdabtesttools.Target;
using log4net;
using Newtonsoft.Json;
using Terradue.Metadata.EarthObservation.OpenSearch.Extensions;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Benchmarking;
using Terradue.OpenSearch.DataHub;
using Terradue.OpenSearch.Engine;
using Terradue.OpenSearch.Result;
using Terradue.ServiceModel.Ogc.Eop21;
using Terradue.ServiceModel.Syndication;

namespace cdabtesttools.TestCases
{
    internal class TestCase304 : TestCase301
    {
        private OfflineDataStatus offlineDataStatus = null;
        private FileInfo offlineDataStatusFile;

        public OfflineDataStatus OfflineDataStatus { get => offlineDataStatus; }
        public TestCase304(ILog log, TargetSiteWrapper target, int load_factor, OfflineDataStatus offlineDataStatus, List<IOpenSearchResultItem> foundItems) :
            base(log, target, foundItems)
        {
            Id = "TC304";
            Title = "Offline Data Download";
            base.load_factor = load_factor;
            this.offlineDataStatus = offlineDataStatus;
            offlineDataStatusFile = new FileInfo("offlineDataStatusFile.json");
            max_try_for_finding_download = 1;
        }

        protected override void CreateDownloadRequestAndEnqueue(IOpenSearchResultItem item)
        {
            log.DebugFormat("Creating download request for {0}...", item.Identifier);
            try
            {
                IAssetAccess enclosureAccess = target.Wrapper.GetEnclosureAccess(item);
                if (enclosureAccess != null)
                {
                    // if (enclosureAccess.GetTotalLength() > target.TargetSiteConfig.MaxDownloadSize)
                    //     throw new Exception(String.Format("Product file too large ({0} > {1}) for {2}", enclosureAccess.GetTotalLength(), target.TargetSiteConfig.MaxDownloadSize, item.Identifier));
                    downloadRequests.Enqueue(enclosureAccess);
                    log.DebugFormat("OK --> {0}", enclosureAccess.Uri);
                }
            }
            catch (ProductArchivingStatusException pase)
            {
                log.WarnFormat("Product {0} is {1}", pase.Item.Identifier, pase.ProductArchivingStatus);
                CreateOrderRequestAndEnqueue(pase);
            }
            catch (Exception e)
            {
                log.WarnFormat("NOT OK: {0}", e.Message);
                log.Debug(e.StackTrace);
            }
        }

        private void CreateOrderRequestAndEnqueue(ProductArchivingStatusException pase)
        {
            OfflineDataStatusItem odsi = null;
            try
            {
                odsi = GetOfflineDataStatus(pase.Item);
                if (pase.ProductArchivingStatus == ProductArchivingStatus.OFFLINE)
                {
                    if (odsi != null)
                    {
                        log.DebugFormat("Product {0} already ordered. Checking status", odsi.Identifier);
                        IAssetAccess enclosureAccess = target.Wrapper.CheckOrder(odsi.OrderId, pase.Item);
                        if (enclosureAccess != null && !(enclosureAccess is ProductOrderEnclosureAccess))
                        {
                            // if (enclosureAccess.GetTotalLength() > target.TargetSiteConfig.MaxDownloadSize)
                            //     throw new Exception(String.Format("Product file too large ({0} > {1}) for {2}", enclosureAccess.GetTotalLength(), target.TargetSiteConfig.MaxDownloadSize, item.Identifier));
                            downloadRequests.Enqueue(enclosureAccess);
                            log.DebugFormat("Order Complete --> {0}", enclosureAccess.Uri);
                        }
                        else
                        {
                            log.DebugFormat("Order not complete for {0}", odsi.Identifier);
                        }
                    }
                    else
                    {
                        log.DebugFormat("Placing order to target site for product {0} ...", pase.Item.Identifier);
                        IAssetAccess productOrder = target.Wrapper.OrderProduct(pase.Item);
                        downloadRequests.Enqueue(productOrder);
                        odsi = OfflineDataStatusItem.Create(pase.Item, target, productOrder.Id);
                        OfflineDataStatus.OfflineData.Add(odsi);
                    }
                }
            }
            catch (Exception poe)
            {
                log.ErrorFormat("Order error: {0}", poe.Message);
                log.Debug(poe.StackTrace);
                if (odsi != null)
                    OfflineDataStatus.OfflineData.Remove(odsi);
            }
        }

        private OfflineDataStatusItem GetOfflineDataStatus(IOpenSearchResultItem item)
        {
            return OfflineDataStatus.OfflineData.FirstOrDefault(odi => odi.TargetSiteName == target.Name && odi.Identifier == item.Identifier);
        }

        protected override List<IOpenSearchResultItem> SearchForMoreItemsToDownload()
        {
            log.WarnFormat("Not enough offline items found for downloading ({0}).", downloadRequests.Count());
            return new List<IOpenSearchResultItem>();
        }


        public override TestCaseResult CompleteTest(Task<IEnumerable<TestUnitResult>> tasks)
        {
            List<IMetric> _testCaseMetric = new List<IMetric>();

            try
            {
                results = tasks.Result.ToList();

                var tcr = MeasurementsAnalyzer.GenerateTestCaseResult(this, new MetricName[]{
                    MetricName.avgResponseTime,
                    MetricName.peakResponseTime,
                    MetricName.errorRate,
                    MetricName.maxSize,
                    MetricName.totalSize,
                    MetricName.resultsErrorRate,
                    MetricName.throughput,
                    MetricName.dataCollectionDivision,
                    MetricName.offlineDataAvailabilityLatency
                }, tasks.Result.Count());

                try
                {
                    RemoveDownloadedOfflineDataStatusFromResults();
                    SaveOfflineDataStatus();
                }
                catch (Exception e)
                {
                    log.WarnFormat("There has been an issue updating the Offline data status : {0}", e.Message);
                    log.Debug(e.StackTrace);
                }

                return tcr;
            }
            catch (AggregateException e)
            {
                log.WarnFormat("Test Case Execution Error : {0}", e.InnerException.Message);
                throw e.InnerException;
            }
        }


        private void RemoveDownloadedOfflineDataStatusFromResults()
        {
            foreach (var result in Results)
            {
                if (result == null || result.Status != TestUnitResultStatus.Complete)
                    continue;
                var enclosureAccess = (IAssetAccess)result.State;
                var _httpStatus = result.Metrics.Where(m => m.Name == MetricName.httpStatusCode).Cast<StringMetric>().Select(m => m.Value);
                int statusCode = int.Parse(_httpStatus.First().Split(':').First());
                if (statusCode == 200)
                {
                    var offlineData = OfflineDataStatus.OfflineData.FirstOrDefault(i => i.Identifier == enclosureAccess.SourceItem.Identifier);
                    OfflineDataStatus.OfflineData.Remove(offlineData);
                }
            }
        }

        internal void SaveOfflineDataStatus()
        {
            log.InfoFormat("Writing Offline Data Status file");
            using (var stream = File.Open(offlineDataStatusFile.FullName, FileMode.Create))
            {
                try
                {
                    StreamWriter sw = new StreamWriter(stream);
                    sw.Write(JsonConvert.SerializeObject(OfflineDataStatus));
                    sw.Close();
                }
                catch (Exception e)
                {
                    log.WarnFormat("Error writing Offline Data Status file : {0}", e.Message);
                }
            }
        }
    }
}
