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
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Benchmarking;
using Terradue.OpenSearch.Request;
using Terradue.OpenSearch.Response;


namespace cdabtesttools.SampleTarget
{

    /// <summary>
    /// Implements an OpenSearch request over HTTP
    /// </summary>
    public class SampleOpenSearchRequest : OpenSearchRequest
    {

        private log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public override NameValueCollection OriginalParameters { get; set; }

        public ICredentials Credentials { get; set; }

        private bool skipCertificateVerification = false;
        
        public bool SkipCertificateVerification
        {
            get
            {
                return skipCertificateVerification;
            }
            set
            {
                skipCertificateVerification = value;

            }
        }

        public int RetryNumber { get; internal set; }

        int timeOut = 10000;

        /// <summary>
        /// Initializes a new instance of the <see cref="Terradue.OpenSearch.Request.SampleOpenSearchRequest"/> class.
        /// </summary>
        internal SampleOpenSearchRequest(OpenSearchUrl url, NameValueCollection originalParameters) : base(url, "application/atom+xml")
        {
            if (!url.Scheme.StartsWith("http"))
                throw new InvalidOperationException("A http scheme is expected for this kind of request");
            this.OpenSearchUrl = url;
            this.OriginalParameters = originalParameters;
        }

        /// <summary>
        /// Gets or sets the HTTP requesttime out.
        /// </summary>
        /// <value>The time out.</value>
        public int TimeOut
        {
            get
            {
                return timeOut;
            }
            set
            {
                timeOut = value;
            }
        }

        /// <summary>
        /// Gets the HTTP response.
        /// </summary>
        /// <returns>The response.</returns>
        public override IOpenSearchResponse GetResponse()
        {

            int retry = RetryNumber;

            while (retry >= 0)
            {
                try
                {

                    byte[] data;
                    MemoryOpenSearchResponse response;

                    HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(this.OpenSearchUrl);

                    if (SkipCertificateVerification)
                    {
                        httpWebRequest.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
                    }

                    httpWebRequest.Accept = "application/atom+xml";

                    httpWebRequest.Timeout = timeOut;
                    httpWebRequest.Proxy = null;
                    httpWebRequest.Credentials = Credentials;
                    httpWebRequest.PreAuthenticate = true;
                    httpWebRequest.AllowAutoRedirect = true;
                    SetBasicAuthHeader(httpWebRequest, (NetworkCredential)Credentials);

                    log.DebugFormat("Querying (Try={1}) {0}", this.OpenSearchUrl, retry);

                    Stopwatch sw2 = new Stopwatch();
                    Stopwatch sw = Stopwatch.StartNew();
                    DateTime beginGetResponseTime = DateTime.UtcNow;
                    DateTime endGetResponseTime = DateTime.UtcNow;

                    return Task.Factory.FromAsync((asyncCallback, state) => {
                         var asyncResult = httpWebRequest.BeginGetResponse(asyncCallback, state);
                         log.DebugFormat("Connected to {0}", this.OpenSearchUrl.Host);
                         beginGetResponseTime = DateTime.UtcNow;
                         sw2.Start();
                         return asyncResult;
                        }, httpWebRequest.EndGetResponse, null)
                    .ContinueWith(resp =>
                    {
                        sw2.Stop();
                        endGetResponseTime = DateTime.UtcNow;
                        log.DebugFormat("Reply from {0}", this.OpenSearchUrl.Host);
                        Metric responseTime = new LongMetric("responseTime", sw.ElapsedMilliseconds, "ms", "Response time of the remote server to answer the query");
                        Metric beginGetResponseTimeMetric = new LongMetric("beginGetResponseTime", beginGetResponseTime.Ticks, "ticks", "Begin time of the get response from remote server to answer the query");
                        Metric endGetResponseTimeMetric = new LongMetric("endGetResponseTime", endGetResponseTime.Ticks, "ticks", "End time of the get response from remote server to answer the query");

                        using (HttpWebResponse webResponse = (HttpWebResponse)resp.Result)
                        {
                            using (var ms = new MemoryStream())
                            {
                                webResponse.GetResponseStream().CopyTo(ms);
                                ms.Flush();
                                data = ms.ToArray();
                            }
                            sw.Stop();
                            Metric requestTime = new LongMetric("requestTime", sw.ElapsedMilliseconds, "ms", "Request time for retrieveing the query");
                            Metric retryNumber = new LongMetric("retryNumber", RetryNumber - retry, "#", "Number of retry to have the query completed");
                            response = new MemoryOpenSearchResponse(data, webResponse.ContentType, new List<Metric>() { responseTime, requestTime, retryNumber });
                        }

                        return response;
                    }).Result;

                }
                catch (AggregateException ae)
                {
                    log.DebugFormat("Error during query at {0} : {1}.", this.OpenSearchUrl, ae.InnerException.Message);
                    if ( ae.InnerException is WebException && ((WebException)ae.InnerException).Response is HttpWebResponse ){
                        var resp = ((WebException)ae.InnerException).Response as HttpWebResponse ;
                        if ( resp.StatusCode == HttpStatusCode.BadRequest ||
                            resp.StatusCode == HttpStatusCode.Unauthorized ||
                            resp.StatusCode == HttpStatusCode.Forbidden ||
                            resp.StatusCode == HttpStatusCode.NotFound ||
                            resp.StatusCode == HttpStatusCode.MethodNotAllowed
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

        public void SetBasicAuthHeader(WebRequest request, NetworkCredential creds)
        {
            if (creds == null) return;
            string authInfo = creds.UserName + ":" + creds.Password;
            authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
            request.Headers["Authorization"] = "Basic " + authInfo;
        }

    }
}

