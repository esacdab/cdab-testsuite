using System;
using System.Collections.Generic;
using NetTopologySuite.Features;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.Data
{
    public class GeometryFilterCollection
    {
        private string key;
        private string ns;
        private IEnumerable<Feature> geometries;

        public string Key => key;

        public IEnumerable<Feature> Features => geometries;

        public GeometryFilterCollection(string key, string ns, IEnumerable<Feature> geometries)
        {
            this.key = key;
            this.ns = ns;
            this.geometries = geometries;
        }
    }
}