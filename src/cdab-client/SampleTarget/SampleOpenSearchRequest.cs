using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terradue.GeoJson.Geometry;
using Terradue.OpenSearch.Benchmarking;
using Terradue.OpenSearch.DataHub.DHuS;
using Terradue.OpenSearch.Request;
using Terradue.OpenSearch.Response;
using Terradue.OpenSearch.Result;
using Terradue.OpenSearch.Sentinel;
using Terradue.OpenSearch.Sentinel.Data;
using Terradue.ServiceModel.Ogc.Eop21;

namespace Terradue.OpenSearch.DataHub.Dias
{
    internal class SampleOpenSearchRequest : OpenSearchRequest
    {

        private log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private SampleWrapper sampleWrapper;
        private QuerySettings querySettings;
        private NameValueCollection parameters;

        public override NameValueCollection OriginalParameters { get => parameters; set => parameters = value; }

        public SampleOpenSearchRequest(SampleWrapper sampleWrapper, QuerySettings querySettings, NameValueCollection parameters) :
        base(null, "application/atom+xml")
        {
            this.sampleWrapper = sampleWrapper;
            this.querySettings = querySettings;
            this.parameters = parameters;
        }

        private MemoryOpenSearchResponse Query()
        {
            CreateQueryUrl();

            int retry = querySettings.MaxRetries;

            while (retry >= 0)
            {
                try
                {
                    byte[] data;

                    HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(this.OpenSearchUrl);

                    httpWebRequest.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

                    ((HttpWebRequest)httpWebRequest).Accept = "application/json";
                    httpWebRequest.Timeout = 120000;
                    httpWebRequest.Proxy = null;
                    // httpWebRequest.Credentials = querySettings.Credentials;
                    // httpWebRequest.PreAuthenticate = true;
                    httpWebRequest.AllowAutoRedirect = true;

                    log.DebugFormat("Querying (Try={1}) {0}", this.OpenSearchUrl, retry);

                    Stopwatch sw = Stopwatch.StartNew();
                    Stopwatch sw2 = new Stopwatch();
                    DateTime beginGetResponseTime = DateTime.UtcNow;
                    DateTime endGetResponseTime = DateTime.UtcNow;

                    return Task.Factory.FromAsync((asyncCallback, state) =>
                    {
                        var asyncResult = httpWebRequest.BeginGetResponse(asyncCallback, state);
                        log.DebugFormat("Connected to {0}", this.OpenSearchUrl.Host);
                        beginGetResponseTime = DateTime.UtcNow;
                        sw2.Start();
                        return asyncResult;
                    }, httpWebRequest.EndGetResponse, null).ContinueWith(resp =>
                    {
                        using (HttpWebResponse webResponse = (HttpWebResponse)resp.Result)
                        {
                            sw2.Stop();
                            endGetResponseTime = DateTime.UtcNow;
                            log.DebugFormat("Reply from {0}", this.OpenSearchUrl.Host);
                            using (var ms = new MemoryStream())
                            {
                                webResponse.GetResponseStream().CopyTo(ms);
                                ms.Flush();
                                data = ms.ToArray();
                            }
                            sw.Stop();
                            Metric requestTime = new LongMetric("requestTime", sw.ElapsedMilliseconds, "ms", "Request time for retrieveing the query");
                            Metric responseTime = new LongMetric("responseTime", sw2.ElapsedMilliseconds, "ms", "Response time for the remote server to answer the request");
                            Metric tryNumber = new LongMetric("retryNumber", querySettings.MaxRetries - retry, "#", "Number of retry to get the query");
                            Metric beginGetResponseTimeTicks = new LongMetric("beginGetResponseTime", beginGetResponseTime.Ticks, "ticks", "Begin time of the get response from remote server to answer the query");
                            Metric endGetResponseTimeTicks = new LongMetric("endGetResponseTime", endGetResponseTime.Ticks, "ticks", "End time of the get response from remote server to answer the query");

                            return new MemoryOpenSearchResponse(data, webResponse.ContentType, new List<Metric>() { responseTime, requestTime, tryNumber, beginGetResponseTimeTicks, endGetResponseTimeTicks });
                        }
                    }).Result;

                }
                catch (AggregateException ae)
                {
                    log.DebugFormat("Error during query at {0} : {1}.", this.OpenSearchUrl, ae.InnerException.Message);
                    if (ae.InnerException is WebException && ((WebException)ae.InnerException).Response is HttpWebResponse)
                    {
                        var resp = ((WebException)ae.InnerException).Response as HttpWebResponse;
                        if (resp.StatusCode == HttpStatusCode.BadRequest ||
                            resp.StatusCode == HttpStatusCode.Unauthorized ||
                            resp.StatusCode == HttpStatusCode.Forbidden ||
                            resp.StatusCode == HttpStatusCode.NotFound ||
                            resp.StatusCode == HttpStatusCode.MethodNotAllowed ||
                            resp.StatusCode == HttpStatusCode.ServiceUnavailable
                        )
                            throw ae.InnerException;
                    }
                    retry--;
                    if (retry > 0)
                    {
                        log.DebugFormat("Retrying in 3 seconds...");
                        Thread.Sleep(3000);
                        continue;
                    }
                    throw ae.InnerException;
                }

            }

            throw new Exception("Unknown error during query at " + this.OpenSearchUrl);
        }

        private void CreateQueryUrl()
        {
            UriBuilder uri = new UriBuilder(sampleWrapper.Settings.ServiceUrl);
            string queryString = "?f=";
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            foreach (var key in parameters.AllKeys)
            {
                switch (key)
                {
                    case "{http://a9.com/-/opensearch/extensions/geo/1.0/}uid":
                        queryString += string.Format("&f=identification.externalId:eq:{0}", parameters[key]);
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform":
                        queryString += string.Format("&f=identification.collection:eq:{0}", parameters[key]);
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}platformSerialIdentifier":
                        queryString += string.Format("&f=acquisition.missionCode:eq:{0}", parameters[key]);
                        break;
                    case "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry":
                    case "{http://a9.com/-/opensearch/extensions/geo/1.0/}box":
                        queryString += string.Format("&gintersect={0}", parameters[key]);
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}productType":
                        queryString += string.Format("&f=identification.type:eq:{0}", parameters[key]);
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}instrument":
                        queryString += string.Format("&f=acquisition.sensorId:eq:{0}", parameters[key]);
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}processingLevel":
                        queryString += string.Format("&f=production.levelCode:eq:{0}", parameters[key]);
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}polarizationChannels":
                        foreach (var pol in parameters[key].Replace("+", " ").Replace("-", " ").Replace("/", " ").Split(' '))
                            queryString += string.Format("&f=acquisition.polarization:eq:{0}", pol);
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}track":
                        if (parameters[key].Contains(","))
                        {
                            queryString += string.Format("&f=orbit.relativeNumber:range:{0}", parameters[key].Replace(",", "<"));
                        }
                        else
                        {
                            queryString += string.Format("&f=orbit.relativeNumber:eq:{0}", parameters[key]);
                        }
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}cloudCover":
                        if (parameters[key].Contains(" TO "))
                        {
                            queryString += string.Format("&f=contentDescription.cloudCoverPercentage:range:{0}", parameters[key].Replace(" TO ", ",").TrimStart('[').TrimEnd(']'));
                        }
                        else
                        {
                            queryString += string.Format("&f=contentDescription.cloudCoverPercentage:lt:{0}", parameters[key]);
                        }
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}statusSubType":
                        if (parameters[key].ToLower() == "online")
                            queryString += string.Format("&f=state.services.download:eq:internal");
                        else if (parameters[key].ToLower() == "offline")
                            queryString += string.Format("&f=state.services.download:eq:external");
                        else
                        {
                            queryString += string.Format("&f=state.services.download:eq:{0}", parameters[key].ToLower());
                        }
                        break;
                    case "{http://purl.org/dc/terms/}modified":
                        if (parameters[key].Contains("/"))
                        {
                            var mods = parameters["{http://purl.org/dc/terms/}modified"].Split('/');
                            DateTime mod1 = DateTime.Parse(mods[0]);
                            DateTime mod2 = DateTime.Parse(mods[1]);
                            queryString += string.Format("&f=timeStamp:gte:{0}", (mod1.ToUniversalTime() - epoch).TotalMilliseconds);
                            queryString += string.Format("&f=timeStamp:lte:{0}", (mod2.ToUniversalTime() - epoch).TotalMilliseconds);
                        }
                        else
                        {
                            DateTime mod = DateTime.Parse(parameters["{http://purl.org/dc/terms/}modified"]);
                            queryString += string.Format("&f=timeStamp:gte:{0}", (mod.ToUniversalTime() - epoch).TotalMilliseconds);
                        }
                        break;
                    case "{http://a9.com/-/spec/opensearch/1.1/}count":
                        queryString += string.Format("&size={0}", parameters[key]);
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}timeliness":
                        if ((!string.IsNullOrEmpty(parameters["{http://a9.com/-/opensearch/extensions/eo/1.0/}platform"])
                        && parameters["{http://a9.com/-/opensearch/extensions/eo/1.0/}platform"] == "Sentinel-3") ||
                        (!string.IsNullOrEmpty(parameters["{http://a9.com/-/opensearch/extensions/eo/1.0/}platformSerialIdentifier"])
                        && parameters["{http://a9.com/-/opensearch/extensions/eo/1.0/}platformSerialIdentifier"].StartsWith("S3")))
                        {
                            queryString += string.Format("&f=identification.externalId:like:*_{0}_*", parameters[key].Substring(0, 2));
                        }
                        break;
                    case "{http://a9.com/-/opensearch/extensions/eo/1.0/}processingMode":
                        if ((!string.IsNullOrEmpty(parameters["{http://a9.com/-/opensearch/extensions/eo/1.0/}platform"])
                        && parameters["{http://a9.com/-/opensearch/extensions/eo/1.0/}platform"].StartsWith("Sentinel-5")) ||
                        (!string.IsNullOrEmpty(parameters["{http://a9.com/-/opensearch/extensions/eo/1.0/}platformSerialIdentifier"])
                        && parameters["{http://a9.com/-/opensearch/extensions/eo/1.0/}platformSerialIdentifier"].StartsWith("S5")))
                        {
                            string value = parameters[key];
                            if (value == "Offline") value = "OFFL";
                            queryString += string.Format("&f=identification.externalId:like:*_{0}_*", value);
                        }
                        break;
                }

            }

            if (!string.IsNullOrEmpty(parameters["{http://a9.com/-/opensearch/extensions/time/1.0/}start"]))
            {
                DateTime start = DateTime.Parse(parameters["{http://a9.com/-/opensearch/extensions/time/1.0/}start"]);
                queryString += string.Format("&f=acquisition.beginViewingDate:gte:{0}", (start.ToUniversalTime() - epoch).TotalMilliseconds);
            }
            if (!string.IsNullOrEmpty(parameters["{http://a9.com/-/opensearch/extensions/time/1.0/}end"]))
            {
                DateTime end = DateTime.Parse(parameters["{http://a9.com/-/opensearch/extensions/time/1.0/}end"]);
                queryString += string.Format("&f=acquisition.endViewingDate:lte:{0}", (end.ToUniversalTime() - epoch).TotalMilliseconds);
            }

            queryString += string.Format("&sort=-timeStamp");

            uri.Query = queryString;

            this.OpenSearchUrl = new OpenSearchUrl(uri.Uri);

        }

        public override IOpenSearchResponse GetResponse()
        {
            MemoryOpenSearchResponse memoryOpenSearchResponse = Query() as MemoryOpenSearchResponse;

            byte[] byteResponse = memoryOpenSearchResponse.GetResponseObject() as byte[];

            JObject json = JsonConvert.DeserializeObject<JObject>(Encoding.Default.GetString(byteResponse));

            JArray hits = json["hits"] as JArray;

            IEnumerable<AtomItem> items = new List<AtomItem>();

            if (hits != null)
            {

                IEnumerable<ISentinelProduct> products = GetProducts(hits);

                SentinelsMetadataGenerator sentinelsMetadataGenerator = new SentinelsMetadataGenerator(sampleWrapper);

                items = sentinelsMetadataGenerator.ProductToAtomItem(products, OriginalParameters);
            }

            AtomFeed feed = new AtomFeed();
            feed.Items = items;

            Metrics metrics = new Metrics() { Metric = memoryOpenSearchResponse.Metrics.ToList() };
            metrics.Metric.Add(new LongMetric("size", byteResponse.Length, "byte"));

            feed.ElementExtensions.Add(metrics.Metric.CreateReader());

            feed.ElementExtensions.Add("totalResults", "http://a9.com/-/spec/opensearch/1.1/", json["totalnb"].Value<string>());

            return new AtomOpenSearchResponse(feed);

        }

        private IEnumerable<ISentinelProduct> GetProducts(JArray hits)
        {
            List<DHuSODataProduct> products = new List<DHuSODataProduct>();
            Terradue.GeoJson.Converter.GeometryConverter converter = new Terradue.GeoJson.Converter.GeometryConverter();
            JsonSerializer serializer = new JsonSerializer();

            foreach (JObject hit in hits)
            {
                DHuSODataProduct product = new DHuSODataProduct();
                try
                {
                    StringReader sr = new StringReader(hit["md"]["geometry"].ToString());
                    Terradue.GeoJson.Geometry.GeometryObject geometry = (Terradue.GeoJson.Geometry.GeometryObject)converter.ReadJson(new JsonTextReader(sr), typeof(Terradue.GeoJson.Geometry.GeometryObject), null, serializer);
                    product.Footprint = geometry.ToWkt();
                }
                catch (Exception e)
                {
                    log.Warn(e.Message);
                }
                JObject data = hit["data"] as JObject;
                product.Name = data["identification"]["externalId"].ToString();
                if (data["acquisition"] != null)
                {
                    if (data["acquisition"]["beginViewingDate"] != null)
                        product.BeginPosition = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(data["acquisition"]["beginViewingDate"].Value<double>());
                    if (data["acquisition"]["endViewingDate"] != null)
                        product.EndPosition = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(data["acquisition"]["endViewingDate"].Value<double>());
                    if (data["acquisition"]["endViewingDate"] != null)
                        product.EndPosition = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(data["acquisition"]["endViewingDate"].Value<double>());
                }
                if (data["orbit"] != null && data["orbit"]["relativeNumber"] != null)
                {
                    product.Properties.Set("relorbit", data["orbit"]["relativeNumber"].Value<string>());
                }
                if (data["archive"] != null && data["archive"]["offLine"] != null)
                {
                    product.Offline = data["archive"]["offLine"].Value<string>();
                }
                product.Id = hit["md"]["id"].ToString();
                product.DownloadUri = new Uri(string.Format("https://sobloo.eu/api/v1/services/download/{0}", product.Id));
                if (data["archive"] != null && data["archive"]["size"] != null)
                    product.ContentLength = long.Parse(data["archive"]["size"].ToString()) * 1024 * 1024;
                if (data["state"] != null && data["state"]["services"] != null && data["state"]["services"]["download"] != null)
                    product.Online = data["state"]["services"]["download"].ToString() == "internal" ? "true" : "false";
                product.IngestionDate = DateTime.UtcNow;
                product.CreationDate = DateTime.UtcNow;
                products.Add(product);
            }

            return products;
        }
    }
}