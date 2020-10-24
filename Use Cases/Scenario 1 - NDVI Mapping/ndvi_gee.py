#!/usr/bin/env python
# Lint as: python3

from __future__ import print_function
import sys
import config
import ee
import time

#
# function to get NDVI Sentinel 2 imagery.
#
def getNDVI(image):
    return image.normalizedDifference(['B8', 'B4'])

#------ Main
def main():
    # Authenticate/Initialize
    #print(config.EE_PRIVATE_KEY_FILE)
    ee.Initialize(config.EE_CREDENTIALS)

    #-------  Init vars
    # Area Of Interest: xMin, yMin, xMax, yMax
    aoi = ee.Geometry.Rectangle(12.3598, 41.7955, 12.6345, 42.0112)

    # start, stop time, clouds
    startTime = '2020-05-07T10:40'
    endTime = '2020-05-07T10:49'
    cloudPercentage = 50

    # SENTINEL-2: 10 m spatial resolution bands: B2 (490 nm), B3 (560 nm), B4 (665 nm) and B8 (842 nm)
    scaleS2 = 20

    # Visualization parameters 
    rgbVis = {
        'min': 0.0,
        'max': 0.3,
        'bands': ['B4', 'B3', 'B2']
    }

    ndviVis = {
        'min': 0.105825, 
        'max': 0.775792
    }

    system_index = '20200507T104031_20200507T104558_T31TFK'
    #-------------------

    # Load Sentinel-2 TOA reflectance data.
    image1 = ee.Image('COPERNICUS/S2/'+system_index)
    
    # get NDVI image
    ndvi1 = getNDVI(image1)
    
    #--- Export image to Drive
    task = ee.batch.Export.image.toDrive(
        image=ndvi1, 
        region=aoi.bounds().getInfo()['coordinates'],
        folder='ES01_results',
        description=system_index + '_NDVI2', 
        fileFormat='GeoTIFF',
        scale=scaleS2)
    task.start()
    
    # check task
    print("Exporting image to Google Drive...")
    while task.active():
        print('Polling for task (id: {0}, status: {1}).'.format(task.id, task.status()))
        time.sleep(5)
        
    print("Finished creating export tasks")
    print("")
    
#
#--- run main
#
main()
