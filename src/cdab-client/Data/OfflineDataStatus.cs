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

        public string Identifier { get; set; }

        public DateTime FirstQueryDateTime { get; set; }

        public DateTime LastQueryUpdateDateTime { get; set; }

        public string TargetSiteName { get; set; }
        public string OrderId { get; set; }
        public string Url { get; set; }

        [JsonIgnore]
        public IOpenSearchResultItem SourceItem { get => sourceItem; }

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