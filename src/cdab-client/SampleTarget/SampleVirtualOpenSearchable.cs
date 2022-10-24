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
using Terradue.OpenSearch;
using Terradue.OpenSearch.Schema;
using Terradue.OpenSearch.DataHub;
using System.Collections.Specialized;
using Terradue.ServiceModel.Syndication;

namespace cdabtesttools.SampleTarget
{

    public class SampleVirtualOpenSearchable : DataHubOpenSearchable
    {

        SampleWrapper sampleWrapper;

        public SampleVirtualOpenSearchable(SampleWrapper wrapper, OpenSearchableFactorySettings settings) : base(wrapper, settings)
        {
            sampleWrapper = wrapper;
        }
        
        public override Terradue.OpenSearch.Request.OpenSearchRequest Create(QuerySettings querySettings, System.Collections.Specialized.NameValueCollection parameters)
        {
            return new SampleVirtualOpenSearchRequest(sampleWrapper, parameters);
        }

    }
}

