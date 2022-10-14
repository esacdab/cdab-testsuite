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
using System.Collections.Specialized;
using System.Xml;
using System.Xml.Serialization;
using Terradue.OpenSearch;
using Terradue.OpenSearch.DataHub;
using Terradue.OpenSearch.Schema;


namespace cdabtesttools.SampleTarget
{

    public class SampleOpenSearchable : DataHubOpenSearchable
    {

        SampleWrapper sampleWrapper;

        public SampleOpenSearchable(SampleWrapper wrapper, OpenSearchableFactorySettings settings) : base(wrapper, settings)
        {
            sampleWrapper = wrapper;
        }

        public override Terradue.OpenSearch.Request.OpenSearchRequest Create(QuerySettings querySettings, System.Collections.Specialized.NameValueCollection parameters)
        {
            string url = null;

            switch (parameters["{http://a9.com/-/opensearch/extensions/eo/1.0/}platform"])
            {
                case "Sentinel-1":
                    url = "https://catalog.terradue.com/sentinel1/search?format=atom&count={count?}&startPage={startPage?}&startIndex={startIndex?}&q={searchTerms?}&lang={language?}&update={dct:modified?}&start={time:start?}&stop={time:end?}&trel={time:relation?}&bbox={geo:box?}&uid={geo:uid?}&geom={geo:geometry?}&rel={geo:relation?}&source={dct:source?}&pt={eop:productType?}&psn={eop:platform?}&psi={eop:platformSerialIdentifier?}&isn={eop:instrument?}&st={eop:sensorType?}&pl={eop:processingLevel?}&ot={eop:orbitType?}&pi={eop:parentIdentifier?}&od={eop:orbitDirection?}&track={eop:track?}&swath={eop:swathIdentifier?}&cc={eop:cloudCover?}&res={eop:sensorResolution?}";
                    break;
                case "Sentinel-2":
                    url = "https://catalog.terradue.com/sentinel2/search?format=atom&count={count?}&startPage={startPage?}&startIndex={startIndex?}&q={searchTerms?}&lang={language?}&update={dct:modified?}&start={time:start?}&stop={time:end?}&trel={time:relation?}&bbox={geo:box?}&uid={geo:uid?}&geom={geo:geometry?}&rel={geo:relation?}&source={dct:source?}&pt={eop:productType?}&psn={eop:platform?}&psi={eop:platformSerialIdentifier?}&isn={eop:instrument?}&st={eop:sensorType?}&pl={eop:processingLevel?}&ot={eop:orbitType?}&od={eop:orbitDirection?}&track={eop:track?}&cc={eop:cloudCover?}&res={eop:sensorResolution?}";
                    break;
                case "Sentinel-3":
                    url = "https://catalog.terradue.com/sentinel3/search?format=atom&count={count?}&startPage={startPage?}&startIndex={startIndex?}&q={searchTerms?}&lang={language?}&update={dct:modified?}&start={time:start?}&stop={time:end?}&trel={time:relation?}&bbox={geo:box?}&uid={geo:uid?}&geom={geo:geometry?}&rel={geo:relation?}&source={dct:source?}&pt={eop:productType?}&psn={eop:platform?}&psi={eop:platformSerialIdentifier?}&isn={eop:instrument?}&st={eop:sensorType?}&pl={eop:processingLevel?}&ot={eop:orbitType?}&od={eop:orbitDirection?}&track={eop:track?}&cycle={eop:cycle?}&cc={eop:cloudCover?}&res={eop:sensorResolution?}";
                    break;
                default:
                    throw new InvalidOperationException("Missing or unsupported platform");
            }

            XmlQualifiedName[] qualifiedNames = new XmlQualifiedName[] {
                new XmlQualifiedName("", "http://a9.com/-/spec/opensearch/1.1/"),
                new XmlQualifiedName("dc", "http://purl.org/dc/elements/1.1/"),
                new XmlQualifiedName("dct", "http://purl.org/dc/terms/"),
                new XmlQualifiedName("eop", "http://a9.com/-/opensearch/extensions/eo/1.0/"),
                new XmlQualifiedName("geo", "http://a9.com/-/opensearch/extensions/geo/1.0/"),
                new XmlQualifiedName("time", "http://a9.com/-/opensearch/extensions/time/1.0/"),
            };

            XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces(qualifiedNames);
            OpenSearchDescriptionUrl templateUrl = new OpenSearchDescriptionUrl("application/atom+xml", url, "search", new System.Xml.Serialization.XmlSerializerNamespaces(namespaces));
            templateUrl.IndexOffset = 0;

            Console.WriteLine("{0} - {1} - {2}", templateUrl == null ? "NULL" : templateUrl.Template, parameters == null ? "NULL" : "PARAMS", querySettings == null ? "NULL" : "QS");
            NameValueCollection fqdnParameters = OpenSearchFactory.BuildFqdnParameterFromTemplate(templateUrl, parameters, querySettings);

            OpenSearchUrl queryUrl = OpenSearchFactory.BuildRequestUrlFromTemplate(templateUrl, AdjustParameters(parameters), querySettings);

            
            return new SampleOpenSearchRequest(queryUrl, fqdnParameters);
        }


        protected NameValueCollection AdjustParameters(NameValueCollection parameters)
        {
            NameValueCollection nvc = new NameValueCollection(parameters);

            string platform = nvc["{http://a9.com/-/opensearch/extensions/eo/1.0/}platform"];

            // Make adjustments to values if there are differences between configuration and target site
            // Remove parameters that are unnecessary or cause problems
            if (platform != null) nvc.Remove("{http://a9.com/-/opensearch/extensions/eo/1.0/}platform");

            return nvc;
        }


    }
}

