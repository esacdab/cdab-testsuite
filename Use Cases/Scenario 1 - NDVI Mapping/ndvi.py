#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
###############################################################################
# How to: Calculate NDVI from Sentinel-2A #
###############################################################################
# @author: Cole Krehbiel # 
# Last Updated: 10-18-17 # 
###############################################################################
"""
# Import libraries
import glob
import sys
import numpy as np
import os

np.seterr(divide='ignore', invalid='ignore')
from osgeo import gdal # If GDAL doesn't recognize jp2 format, check version</pre>
# Set input directory
print(sys.argv[1])
in_dir = sys.argv[1]
 
# Search directory for desired bands
red_file = glob.glob(in_dir + '/**B04.jp2') # red band
nir_file = glob.glob(in_dir + '/**B08.jp2') # nir band

# Define a function to calculate NDVI using band arrays for red, NIR bands
def ndvi(red, nir):
 return ((nir - red)/(nir + red))
 
# Open each band using gdal
red_link = gdal.Open(red_file[0])
nir_link = gdal.Open(nir_file[0])
 
# read in each band as array and convert to float for calculations
red = red_link.ReadAsArray().astype(np.float)
nir = nir_link.ReadAsArray().astype(np.float)
 
# Call the ndvi() function on red, NIR bands
ndvi2 = ndvi(red, nir)
 
# Create output filename based on input name
out_dir = os.getcwd()
if len(sys.argv) > 2:
    out_dir = sys.argv[2]
outfile_name = out_dir + "/" + os.path.basename(red_file[0].split('_B')[0]) + '_NDVI.tif'
 
x_pixels = ndvi2.shape[0] # number of pixels in x
y_pixels = ndvi2.shape[1] # number of pixels in y
 
# Set up output GeoTIFF
driver = gdal.GetDriverByName('GTiff')
 
# Create driver using output filename, x and y pixels, # of bands, and datatype
ndvi_data = driver.Create(outfile_name,x_pixels, y_pixels, 1,gdal.GDT_Float32)
 
# Set NDVI array as the 1 output raster band
ndvi_data.GetRasterBand(1).WriteArray(ndvi2)
 
# Setting up the coordinate reference system of the output GeoTIFF
geotrans=red_link.GetGeoTransform() # Grab input GeoTranform information
proj=red_link.GetProjection() # Grab projection information from input file
 
# now set GeoTransform parameters and projection on the output file
ndvi_data.SetGeoTransform(geotrans) 
ndvi_data.SetProjection(proj)
ndvi_data.FlushCache()
ndvi_data=None
###############################################################################