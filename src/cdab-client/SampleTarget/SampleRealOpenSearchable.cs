using System;
using System.Linq;
using Terradue.OpenSearch.Schema;
using System.Collections.Specialized;
using Terradue.ServiceModel.Syndication;

namespace Terradue.OpenSearch.DataHub.Dias
{

    public class SampleRealOpenSearchable : DataHubOpenSearchable
    {

        SampleWrapper sampleWrapper;

        public SampleRealOpenSearchable(SampleWrapper wrapper, OpenSearchableFactorySettings settings) :
        base(wrapper, settings)
        {
            sampleWrapper = wrapper;
        }
        
        public override System.Collections.Specialized.NameValueCollection GetOpenSearchParameters(string mimeType)
        {
            // This gets a NameValueCollection of the basic OpenSearch parameters
            // that the search has to support:
            // count, startIndex, startPage, searchTerms, lang
            NameValueCollection osdic = OpenSearchFactory.GetBaseOpenSearchParameter();

            // Here specific search parameters are added:
            osdic.Add("uid", "{geo:uid?}");
            osdic.Add("psi", "{eo:platformSerialIdentifier?}");
            osdic.Add("psn", "{eo:platform?}");
            osdic.Add("isn", "{eo:instrument?}");
            osdic.Add("geom", "{geo:geometry?}");
            osdic.Add("start", "{time:start?}");
            osdic.Add("end", "{time:end?}");
            osdic.Add("pt", "{eo:productType?}");
            osdic.Add("modified", "{dct:modified?}");
            osdic.Add("timeliness", "{eo:timeliness?}");
            osdic.Add("pl", "{eo:processingLevel?}");
            osdic.Add("polc", "{eo:polarizationChannels?}");
            osdic.Add("pm", "{eo:processingMode?}");
            osdic.Add("track", "{eo:track?}");
            osdic.Add("cc", "{eo:cloudCover?}");
            return osdic;
        }


        public override Terradue.OpenSearch.Request.OpenSearchRequest Create(QuerySettings querySettings, System.Collections.Specialized.NameValueCollection parameters)
        {
            OpenSearchDescription osd = this.GetOpenSearchDescription();

            OpenSearchDescriptionUrl templateUrl = OpenSearchFactory.GetOpenSearchUrlByType(osd, querySettings.PreferredContentType);

            if (templateUrl == null) throw new InvalidOperationException(string.Format("Could not find a URL template for entity {0} with type {1}", this.Identifier, querySettings.PreferredContentType));

            // 1/ put everything FQDN
            var fqdnParameters = OpenSearchFactory.BuildFqdnParameterFromTemplate(templateUrl, parameters, querySettings);

            // 2/ find the right collection from all params
            return new SampleOpenSearchRequest(sampleWrapper, querySettings, fqdnParameters);

        }

        protected NameValueCollection AdjustParameters(NameValueCollection parameters)
        {
            NameValueCollection nvc = new NameValueCollection(parameters);

            // Make adjustments to values 

            return MapParameters(nvc);
        }
    }
}

