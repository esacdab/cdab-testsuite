using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace cdabtesttools.Data
{
    internal class ShapeFileLoader
    {
        internal static IEnumerable<Feature>  Load(string v)
        {
            var shapeFileDataReader = Shapefile.CreateDataReader(v, new GeometryFactory());
            ShapefileHeader shpHeader = shapeFileDataReader.ShapeHeader;
            DbaseFileHeader header = shapeFileDataReader.DbaseHeader;
            shapeFileDataReader.Reset();

            //Read through all records of the shapefile (geometry and attributes) into a feature collection 

            List<Feature> features = new List<Feature>();
            int j = 1;
            while (shapeFileDataReader.Read())
            {
                Feature feature = new Feature();
                AttributesTable attributesTable = new AttributesTable();
                string[] keys = new string[header.NumFields];
                var pm = new PrecisionModel(10.0);
                var pop = new NetTopologySuite.Precision.GeometryPrecisionReducer(pm);
                Geometry geometry = NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(pop.Reduce((Geometry)shapeFileDataReader.Geometry), 0.5);
                // geometry = NetTopologySuite.Operation.BoundaryOp.GetBoundary(geometry);
                // var pol = new  NetTopologySuite.Operation.Polygonize.Polygonizer();
                // pol.Add()
                if (geometry.IsEmpty)
                    continue;
                for (int i = 0; i < header.NumFields; i++)
                {
                    DbaseFieldDescriptor fldDescriptor = header.Fields[i];
                    keys[i] = fldDescriptor.Name;
                    attributesTable.Add(fldDescriptor.Name, shapeFileDataReader.GetValue(i+1));

                }
                if (!attributesTable.GetNames().Contains("NAME", StringComparer.InvariantCulture))
                    attributesTable.Add("NAME", j);
                feature.Geometry = geometry;
                feature.Attributes = attributesTable;
                features.Add(feature);
                j++;
            }

            //Close and free up any resources
            shapeFileDataReader.Close();
            shapeFileDataReader.Dispose();

            return features;
        }
    }
}