#!/usr/bin/env python3
import sys
import numpy as np
import os
from osgeo import gdal

# Read command-line arguments
# Usage: ndvi.py <input_product_dir> <output_dir>
input_dir = sys.argv[1]

if len(sys.argv) >= 3:
    output_dir = sys.argv[2]
else:
    output_dir = os.getcwd()

bands = {'B04.jp2': None, 'B08.jp2': None}

for root, dirs, files in os.walk('.'):
    for f in files:
        for b in bands:
            if b == f[-7:]:
                bands[b] = '{0}/{1}'.format(root, f)

red_file = bands['B04.jp2']   # red band
nir_file = bands['B08.jp2']   # NIR band

print("RED: {0}".format(red_file))
print("NIR: {0}".format(nir_file))

if not red_file or not nir_file:
    sys.exit(1)

red_data = gdal.Open(red_file)
nir_data = gdal.Open(nir_file)

# Read bands as float arrays
red = red_data.ReadAsArray().astype(np.float)
nir = nir_data.ReadAsArray().astype(np.float)
 
# Calculate NDVI
np.seterr(divide='ignore', invalid='ignore')
ndvi = (nir - red) / (nir + red)
 
# Create output filename based on input name
outfile_name = "{0}/{1}_NDVI.tif".format(output_dir, os.path.basename(red_file)[:22])
 
width = ndvi.shape[0]
height = ndvi.shape[1]
 
# Set up output GeoTIFF
driver = gdal.GetDriverByName('GTiff')
 
# Create driver using output filename, x and y pixels, # of bands, and datatype
ndvi_data = driver.Create(outfile_name, width, height, 1, gdal.GDT_Float32)
 
ndvi_data.GetRasterBand(1).WriteArray(ndvi)

# Obtain coordinate reference system information
geo_transform = red_data.GetGeoTransform()
projection = red_data.GetProjection()
 
# Set GeoTransform parameters and projection on the output file
ndvi_data.SetGeoTransform(geo_transform) 
ndvi_data.SetProjection(projection)
ndvi_data.FlushCache()
ndvi_data = None
