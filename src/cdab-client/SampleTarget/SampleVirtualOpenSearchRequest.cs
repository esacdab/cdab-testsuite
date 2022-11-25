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
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Xml;
using System.Text;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Benchmarking;
using Terradue.OpenSearch.Request;
using Terradue.OpenSearch.Response;
using Terradue.OpenSearch.Result;
using Terradue.ServiceModel.Syndication;
using Terradue.GeoJson.Geometry;
using Terradue.GeoJson.Gml311;
using Terradue.ServiceModel.Ogc.Gml311;
using Terradue.ServiceModel.Ogc.GeoRss.GeoRss;


namespace cdabtesttools.SampleTarget
{

    /// <summary>
    /// Implements a request over HTTP that behaves similarly to an OpenSearch request
    /// </summary>
    public class SampleVirtualOpenSearchRequest : OpenSearchRequest
    {
        private log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ICredentials Credentials { get; set; }

        public override NameValueCollection OriginalParameters { get; set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="Terradue.OpenSearch.Request.SampleVirtualOpenSearchRequest"/> class.
        /// </summary>
        internal SampleVirtualOpenSearchRequest(SampleWrapper wrapper, NameValueCollection originalParameters) : base(null, "application/atom+xml")
        {
            this.OriginalParameters = originalParameters;
            this.OpenSearchUrl = new OpenSearchUrl(wrapper.Settings.ServiceUrl);
        }

        /// <summary>
        /// Gets the HTTP response.
        /// </summary>
        /// <returns>The response.</returns>
        public override IOpenSearchResponse GetResponse()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            // At thiis the query request should happen, taking into account the values in originalParameters
            // (a NameValueCollection of fully-qualified parameter keys and their search values)
            // In this sample, the data is taken from some predefined structure (containing just one record)
            
            // That data then needs to be transformed into an ATOM feed
            SearchResult result = GetTestSearchResult();

            List<AtomItem> items = new List<AtomItem>();

            // Creates an ATOM item for every product found
            foreach (SearchResultProduct product in result.Products) {
                
                AtomItem item = new AtomItem();

                // lastupdate, publishdate, id
                item.LastUpdatedTime = product.LastUpdatedTime;
                item.PublishDate = product.OnlineDate;
                item.Id = product.Id;
                item.Identifier = product.Id;
                item.Title = new TextSyndicationContent(product.Id);

                // date
                if (product.Date.Ticks != 0) {
                    string dateStr = String.Format("{0:O}", product.Date.ToUniversalTime());
                    item.ElementExtensions.Add("date", "http://purl.org/dc/elements/1.1/", String.Format("{0:yyyy-MM-ddTHH:mm:ssZ}/{0:yyyy-MM-ddTHH:mm:ssZ}", dateStr));
                }
                
                // spatial wkt
                if (product.GeometryGml != null) {
                    try
                    {
                        byte[] byteArray = Encoding.ASCII.GetBytes(product.GeometryGml);
                        MemoryStream stream = new MemoryStream(byteArray);
                        var gml = GmlHelper.Deserialize(XmlReader.Create(stream));
                        var geometry = gml.ToGeometry();
                        if (geometry != null)
                        {
                            item.ElementExtensions.Add(geometry.ToGeoRss().CreateReader());
                            item.ElementExtensions.Add("spatial", "http://purl.org/dc/terms/", geometry.ToWkt());
                        }
                    }
                    catch { }
                }

                // Enclosure links, adjust title, length, content type, download URI
                if (product.DownloadUrl != null) {
                    SyndicationLink enclosureLink = new SyndicationLink();
                    enclosureLink.RelationshipType = "enclosure";
                    enclosureLink.Title = "Download link";
                    //enclosureLink.Length = 0;
                    enclosureLink.MediaType = "appliication/octet-stream";
                    string link = product.DownloadUrl;
                    enclosureLink.Uri = new Uri(link);
                    item.Links.Add(enclosureLink);
                }
                
                items.Add(item);
            }

            AtomFeed feed = new AtomFeed();
            feed.Items = items;

            sw.Stop();

            // For result-related metrics, see below (SearchResult class)
            Metrics metrics = new Metrics();
            metrics.Metric.Add(new LongMetric("requestTime", sw.ElapsedMilliseconds, "ms"));
            metrics.Metric.Add(new LongMetric("retryNumber", result.LastRetryNumber, "#"));
            metrics.Metric.Add(new LongMetric("beginGetResponseTime", result.LastBeginGetResponseTime.Ticks, "ticks", "Begin time of the get response from remote server to answer the query"));
            metrics.Metric.Add(new LongMetric("endGetResponseTime", result.LastEndGetResponseTime.Ticks, "ticks", "End time of the get response from remote server to answer the query"));
            metrics.Metric.Add(new LongMetric("responseTime", result.LastResponseTime, "ms"));
            metrics.Metric.Add(new LongMetric("size", result.LastQueryResultSize, "byte"));
            feed.ElementExtensions.Add(metrics.Metric.CreateReader());

            feed.ElementExtensions.Add("totalResults", "http://a9.com/-/spec/opensearch/1.1/", result.TotalResults);

            return new AtomOpenSearchResponse(feed);
        }


        public static SearchResult GetTestSearchResult()
        {
            SearchResult result = new SearchResult()
            {
                LastRetryNumber = 0,
                LastBeginGetResponseTime = DateTime.UtcNow.AddSeconds(-10),
                LastEndGetResponseTime = DateTime.UtcNow.AddSeconds(-9),
                LastResponseTime = 10000,
            };

            SearchResultProduct product = new SearchResultProduct()
            {
                Id = "TESTPRODUCT",
                Date = DateTime.UtcNow.AddHours(-24),
                GeometryGml = @"<gml:MultiSurface><gml:surfaceMembers><gml:Polygon><gml:exterior><gml:LinearRing><gml:posList count=""5"">10 10 10 20 20 20 20 10 10 10</gml:posList></gml:LinearRing></gml:exterior></gml:Polygon></gml:surfaceMembers></gml:MultiSurface>",
                DownloadUrl = "https://www.terradue.com/wp-content/uploads/2017/03/home2-2.jpg",

                LastUpdatedTime = DateTime.UtcNow.AddHours(-1),
                OnlineDate = DateTime.UtcNow.AddHours(-2),
            };

            result.Products = new List<SearchResultProduct>() { product };

            return result;

        }

    }



    // Sample class to represent a target-site specific search result
    // This class contains metrics that are measured during the request
    public class SearchResult
    {
        // Note: "Last..." refers to the last request performed in the case of retries.
        // I.e. it refers to the first successful request.

        // This should contain the retry number of bytes of the last request
        // (0 if the first attempt was successful)
        public long LastRetryNumber { get; set; }
        
        // This should contain the time when the request was sent
        public DateTime LastBeginGetResponseTime { get; set; }

        // This should contain the time when the server responded (before anything is read)
        public DateTime LastEndGetResponseTime { get; set; }

        // This should contain the response time in millisconds of the request
        public long LastResponseTime { get; set; }

        // This should contain the content length of the result in bytes of the request
        public long LastQueryResultSize { get; set; }

        // This should contain the total number of results matching the search criteria
        // In the sample case, there is only one result page with one result item
        public long TotalResults { get { return Products.Count; } }

        // This should contain the result items (products)
        public List<SearchResultProduct> Products { get; set; }
    }

    // Sample class to represent a target-site specific product item in a search result
    public class SearchResultProduct
    {
        public string Id { get; set; }
        public DateTime Date { get; set; }
        public string GeometryGml { get; set; }
        public string DownloadUrl { get; set; }

        public DateTime LastUpdatedTime { get; set; }
        public DateTime OnlineDate { get; set; }
    }

}

