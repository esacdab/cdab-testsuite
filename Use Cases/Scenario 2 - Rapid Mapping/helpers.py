import os
import sys
sys.path.append('/opt/anaconda/envs/env_s3/snap/.snap/snap-python')
os.environ['GPT_BIN'] = '/opt/anaconda/envs/env_s3/snap/bin/gpt'

import gdal
import numpy as np

import math

from py_snap_helpers import *

def analyse(row, aoi, data_path):
    
    if aoi is not None:
        aoi_intersection = (aoi.intersection(row['wkt']).area / aoi.area) * 100
    else:
        aoi_intersection = np.nan
        
    row['aoi_intersection'] = aoi_intersection
    row['utm_zone'] = row['identifier'][39:41]
    row['latitude_band'] = row['identifier'][41]
    row['grid_square']  = row['identifier'][42:44]
    row['local_path'] = os.path.join(data_path, row['identifier'])
    

def get_band_path(row, band):
    
    ns = {'xfdu': 'urn:ccsds:schema:xfdu:1',
          'safe': 'http://www.esa.int/safe/sentinel/1.1',
          'gml': 'http://www.opengis.net/gml'}
    
    path_manifest = os.path.join(row['local_path'],
                                 row['identifier'] + '.SAFE', 
                                'manifest.safe')
    
    root = etree.parse(path_manifest)
    
    bands = [band]

    for index, band in enumerate(bands):

        sub_path = os.path.join(row['local_path'],
                                row['identifier'] + '.SAFE',
                                root.xpath('//dataObjectSection/dataObject/byteStream/fileLocation[contains(@href,("%s%s")) and contains(@href,("%s")) ]' % (row['latitude_band'],
                                row['grid_square'], 
                                band), 
                                  namespaces=ns)[0].attrib['href'][2:])
    
    return sub_path



def pre_processing(**kwargs):
   
    options = dict()
    
    operators = ['Read', 
                 'Resample',
                 'BandMaths',
                 'Reproject',
                 'Subset',
                 'Write']
    
    for operator in operators:
            
        print('Getting default values for Operator {0}'.format(operator))
        parameters = get_operator_default_parameters(operator)
        
        options[operator] = parameters

    for key, value in kwargs.items():
        
        print('Updating Operator {0}'.format(key))
        options[key.replace('_', '-')].update(value)
    
    mygraph = GraphProcessor(os.environ['GPT_BIN'])
    
    for index, operator in enumerate(operators):
    
        print('Adding Operator {0} to graph'.format(operator))
        if index == 0:            
            source_node_id = ''
        
        else:
            source_node_id = operators[index - 1]
        
        mygraph.add_node(operator,
                         operator, 
                         options[operator], source_node_id)
    
    mygraph.view_graph()
    mygraph.save_graph('pre_graph.xml')
    
    mygraph.run()
    
    
def burned_area(**kwargs):
   
    options = dict()
    
    operators = ['Read', 
                 'BandMaths',
                 'Write']
    
    for operator in operators:
            
        print('Getting default values for Operator {0}'.format(operator))
        parameters = get_operator_default_parameters(operator)
        
        options[operator] = parameters

    for key, value in kwargs.items():
        
        print('Updating Operator {0}'.format(key))
        options[key.replace('_', '-')].update(value)
    
    mygraph = GraphProcessor(os.environ['GPT_BIN'])
    
    for index, operator in enumerate(operators):
    
        print('Adding Operator {0} to graph'.format(operator))
        if index == 0:            
            source_node_id = ''
        
        else:
            source_node_id = operators[index - 1]
        
        mygraph.add_node(operator,
                         operator, 
                         options[operator], source_node_id)
    
    mygraph.view_graph()
    
    mygraph.run()
    
    

def create_rgba(input_tif, output_rgb):
    
    ds = gdal.Open(input_tif)
    
    band = ds.GetRasterBand(1).ReadAsArray()
    
    width = ds.RasterXSize
    height = ds.RasterYSize
    input_geotransform = ds.GetGeoTransform()
    input_georef = ds.GetProjectionRef()

    red_band = np.zeros((height, width))
    green_band = np.zeros((height, width))
    blue_band = np.zeros((height, width))
    alpha_band = np.zeros((height, width))
    
    red_band[np.where(band >= -1.25)] = 159
    red_band[np.where(band >= -0.75)] = 43
    red_band[np.where(band >= -0.375)] = 139
    red_band[np.where(band >= -0.175)] = 97
    red_band[np.where(band >= 0)] = 250
    red_band[np.where(band >= 0.184)] = 228
    red_band[np.where(band >= 0.354)] = 202
    red_band[np.where(band >= 0.549)] = 82


    green_band[np.where(band >= -1.25)] = 159
    green_band[np.where(band >= -0.75)] = 25
    green_band[np.where(band >= -0.375)] = 221
    green_band[np.where(band >= -0.175)] = 169
    green_band[np.where(band >= 0)] = 254
    green_band[np.where(band >= 0.184)] = 173
    green_band[np.where(band >= 0.354)] = 59
    green_band[np.where(band >= 0.549)] = 15

    
    blue_band[np.where(band >= -1.25)] = 159
    blue_band[np.where(band >= -0.75)] = 223
    blue_band[np.where(band >= -0.375)] = 231
    blue_band[np.where(band >= -0.175)] = 45
    blue_band[np.where(band >= 0)] = 76
    blue_band[np.where(band >= 0.184)] = 55
    blue_band[np.where(band >= 0.354)] = 18
    blue_band[np.where(band >= 0.549)] = 112


    alpha_band[np.where(band != -999)] = 255

    
    driver = gdal.GetDriverByName('GTiff')
    try:
        
        output = driver.Create(output_rgb, 
                           width, 
                           height, 
                           4, 
                           gdal.GDT_Byte)
        output.SetGeoTransform(input_geotransform)
        output.SetProjection(input_georef)
        output.GetRasterBand(1).WriteArray(red_band)
        output.GetRasterBand(2).WriteArray(green_band)
        output.GetRasterBand(3).WriteArray(blue_band)
        output.GetRasterBand(4).WriteArray(alpha_band)
        output.FlushCache()
        return True
    except: 
        return False