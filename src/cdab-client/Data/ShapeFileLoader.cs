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
