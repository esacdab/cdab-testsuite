using System;
using System.Collections.Generic;
using NetTopologySuite.Features;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.Data
{
    public class GeometryFilterCollection
    {
        private string key;

        public string Key
        {
            get
            {
                return key;
            }
        }

        private string ns;
        private IEnumerable<Feature> geometries;

        public IEnumerable<Feature> Features
        {
            get
            {
                return geometries;
            }
        }


        public GeometryFilterCollection(string key, string ns, IEnumerable<Feature> geometries)
        {
            this.key = key;
            this.ns = ns;
            this.geometries = geometries;
        }
    }
}