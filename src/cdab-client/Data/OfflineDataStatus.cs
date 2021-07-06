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
using System.Collections.Generic;
using System.Linq;
using cdabtesttools.Target;
using Newtonsoft.Json;
using Terradue.OpenSearch.DataHub;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.Data
{
    public class OfflineDataStatus
    {
        public OfflineDataStatus()
        {
            OfflineData = new List<OfflineDataStatusItem>();
        }

        public List<OfflineDataStatusItem> OfflineData { get; set; }
    }


    public class OfflineDataStatusItem
    {
        private IOpenSearchResultItem sourceItem;

        public string Identifier { get; set; }

        public DateTime FirstQueryDateTime { get; set; }

        public DateTime LastQueryUpdateDateTime { get; set; }

        public string TargetSiteName { get; set; }

        public string OrderId { get; set; }

        public string Url { get; set; }

        [JsonIgnore]
        public IOpenSearchResultItem SourceItem { get => sourceItem; }

        public OfflineDataStatusItem()
        {
            FirstQueryDateTime = DateTime.UtcNow;
            LastQueryUpdateDateTime = FirstQueryDateTime;
        }

        public OfflineDataStatusItem(IOpenSearchResultItem i) : this()
        {
            Identifier = i.Identifier;
            var link = i.Links.FirstOrDefault(l => l.RelationshipType == "enclosure");
            if (link != null)
                Url = link.Uri.ToString();
        }

        public IAssetAccess GetEnclosureAccess(TargetSiteWrapper target)
        {
            if (sourceItem == null)
            {
                FiltersDefinition fd = new FiltersDefinition("offline");
                fd.AddFilter("uid", "{http://a9.com/-/opensearch/extensions/geo/1.0/}uid", Identifier, Identifier, null, null);
                var os = target.CreateOpenSearchableEntity(fd);
                
            }

            return target.Wrapper.GetEnclosureAccess(sourceItem);

        }

        internal static OfflineDataStatusItem Create(IOpenSearchResultItem i, TargetSiteWrapper target, string orderId)
        {
            var odsi = new OfflineDataStatusItem(i);
            odsi.sourceItem = i;
            odsi.TargetSiteName = target.Name;
            odsi.OrderId = orderId;
            return odsi;
        }
    }
}
