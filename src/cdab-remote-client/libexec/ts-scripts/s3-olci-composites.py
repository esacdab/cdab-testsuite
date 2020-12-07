import os
import sys
from py_snap_helpers import *
import os
import sys
import geopandas as gp
import pandas as pd
import numpy as np
sys.path.append(os.getcwd())
sys.path.append('/opt/OTB/lib/python')
sys.path.append('/opt/OTB/lib/libfftw3.so.3')
os.environ['OTB_APPLICATION_PATH'] = '/opt/OTB/lib/otb/applications'
os.environ['LD_LIBRARY_PATH'] = '/opt/OTB/lib'
os.environ['ITK_AUTOLOAD_PATH'] = '/opt/OTB/lib/otb/applications'
import otbApplication
import gdal
from shapely.wkt import loads
from shapely.geometry import box
import shutil
import math
import re
import xml.etree.ElementTree as etree


def get_mask(idepix, classif_flags):
    
    pixel_classif_flags = {
        'IDEPIX_BRIGHT': 128,
        'IDEPIX_CLOUD': 2,
        'IDEPIX_CLOUD_AMBIGUOUS': 4,
        'IDEPIX_CLOUD_BUFFER': 16,
        'IDEPIX_CLOUD_SHADOW': 32,
        'IDEPIX_CLOUD_SURE': 8,
        'IDEPIX_COASTLINE': 512,
        'IDEPIX_INVALID': 1,
        'IDEPIX_LAND': 1024,
        'IDEPIX_SNOW_ICE': 64,
        'IDEPIX_WHITE': 256
    }
    
    
    b1 = int(math.log(pixel_classif_flags[idepix], 2))
    b2 = b1
    
    return _capture_bits(classif_flags.astype(np.int64), b1, b2)

def _capture_bits(arr, b1, b2):
    
    width_int = int((b1 - b2 + 1) * "1", 2)
 
    return ((arr >> b2) & width_int).astype('uint8')

def export_s3(bands):

    ds = gdal.Open(bands[0])
    
    width = ds.RasterXSize
    height = ds.RasterYSize

    input_geotransform = ds.GetGeoTransform()
    input_georef = ds.GetProjectionRef()
    
    ds = None
    
    driver = gdal.GetDriverByName('GTiff')
    
    output = driver.Create(
        's3.tif', 
        width, 
        height, 
        len(bands), 
        gdal.GDT_Float32
    )

    output.SetGeoTransform(input_geotransform)
    output.SetProjection(input_georef)
    
    for index, band in enumerate(bands):
        print(band)
        temp_ds = gdal.Open(band) 
        
        band_data = temp_ds.GetRasterBand(1).ReadAsArray()
        output.GetRasterBand(index+1).WriteArray(band_data)
        
    output.FlushCache()
    
    return True

def read_s3(bands):

    gdal.UseExceptions()
    
    stack = []
    
    for index, band in enumerate(bands):
        
        temp_ds = gdal.Open(band) 
 
        if not temp_ds:
            raise ValueError()
            
        stack.append(temp_ds.GetRasterBand(1).ReadAsArray())
      
    return np.dstack(stack)


def s3_olci_import(idepix, **kwargs):
   
    options = dict()
    
    operators = [
        'Read', 
        'Idepix.Sentinel3.Olci',
        'Reproject',
        'Write'
    ]
    
    for operator in operators:
        print 'Getting default values for Operator {}'.format(operator)
        parameters = get_operator_default_parameters(operator)
        
        options[operator] = parameters

    for key, value in kwargs.items():
        print 'Updating Operator {}'.format(key)
        options[key.replace('_', '-')].update(value)
     
    mygraph = GraphProcessor()
    
    for index, operator in enumerate(operators):
        print 'Adding Operator {} to graph'.format(operator)
        if index == 0:            
            source_node_id = ''
        else:
            source_node_id = operators[index - 1]
       
        if operator == 'Idepix.Sentinel3.Olci':
            mygraph.add_node(operator, operator, idepix, source_node_id)
        else:
            mygraph.add_node(operator, operator, options[operator], source_node_id)
    
    mygraph.run()
    
    
def s3_rgb_composite(red, green, blue, classif_flags, geo_transform, projection_ref, output_name):

    rgb_r = np.zeros(red.shape)
    rgb_g = np.zeros(red.shape)
    rgb_b = np.zeros(red.shape)
    
    mask_cloud = get_mask('IDEPIX_CLOUD', classif_flags)

    mask = (red == -10000) | (green == -10000) | (blue == -10000) | (red > 1) | (green > 1) | (blue > 1) 
    
    rgb_r = np.where(mask, 0, red*255).astype(np.uint8)
    
    rgb_g = np.where(mask, 0, green*255).astype(np.uint8)
    
    rgb_b = np.where(mask, 0, blue*255).astype(np.uint8)
    
    alpha = np.where(mask, 0, 255).astype(int)

    # contrast enhancement
    ContrastEnhancement = otbApplication.Registry.CreateApplication('ContrastEnhancement')

    rgb_data = np.dstack([rgb_r, rgb_g, rgb_b])
    
    ContrastEnhancement.SetVectorImageFromNumpyArray('in', rgb_data)
    
    ContrastEnhancement.SetParameterOutputImagePixelType('out', 
                                                         otbApplication.ImagePixelType_uint8)
    ContrastEnhancement.SetParameterFloat('nodata', 0.0)
    ContrastEnhancement.SetParameterFloat('hfact', 3.0)
    ContrastEnhancement.SetParameterInt('bins', 256)
    ContrastEnhancement.SetParameterInt('spatial.local.w', 500)
    ContrastEnhancement.SetParameterInt('spatial.local.h', 500)
    ContrastEnhancement.SetParameterString('mode', 'lum')

    ContrastEnhancement.Execute()

    ce_data = ContrastEnhancement.GetVectorImageAsNumpyArray('out')
            
    rgb_r = None
    rgb_g = None
    rgb_b = None

    driver = gdal.GetDriverByName('GTiff')

    output = driver.Create(output_name, ce_data.shape[1], ce_data.shape[0], 4, gdal.GDT_Byte)

    output.SetGeoTransform(geo_transform)
    output.SetProjection(projection_ref)
    output.GetRasterBand(1).WriteArray(ce_data[:,:,0])
    output.GetRasterBand(2).WriteArray(ce_data[:,:,1])
    output.GetRasterBand(3).WriteArray(ce_data[:,:,2])
    output.GetRasterBand(4).WriteArray(alpha)
    
    output.FlushCache()
    
    return rgb_r, rgb_g, rgb_b, alpha


def extract_info(atom_file):
    namespaces = {
        'atom': 'http://www.w3.org/2005/Atom',
        'dc': 'http://purl.org/dc/elements/1.1/',
        'dct': 'http://purl.org/dc/terms/',
    }

    feed = etree.fromstring(open(atom_file, 'r').read())
    entry = feed.find('./atom:entry', namespaces)

    result = {}
    date = entry.find('./dc:date', namespaces).text
    date_match = re.match('(.*)/(.*)', date)
    if date_match:
        result['startdate'] = date_match.group(1)
        result['enddate'] = date_match.group(2)
    result['wkt'] = entry.find('./dct:spatial', namespaces).text

    return [ result ]


# Get parameters

atom_file = sys.argv[1]
sen_folder = sys.argv[2]

s3_file = "{0}/xfdumanifest.xml".format(sen_folder)

input_metadata = gp.GeoDataFrame(
    extract_info(atom_file)
)

input_metadata['geometry'] = input_metadata['wkt'].apply(loads)
input_metadata['startdate'] = pd.to_datetime(input_metadata['startdate'])
input_metadata['enddate'] = pd.to_datetime(input_metadata['enddate'])




natural_colors = {
    'id': 'natural_colors',
    'title': 'Natural colors (Oa08, Oa06, Oa_04)',
    'abstract': 'Natural colors (Oa08, Oa06, Oa_04)',
    'value': 'Yes',
    'options': 'Yes,No'
}

gdal.UseExceptions()


# Check the bands to ingest

composites = dict()

composites['S3 OLCI Natural Colors'] = {
    'bands': 'Oa08_reflectance,Oa06_reflectance,Oa04_reflectance', 
    'create': True if (natural_colors['value'] == 'Yes') else False,
    'hfact': 2.0
}

s3_olci_bands = []

for key, value in composites.iteritems():
    if value['create']:
        for band in value['bands'].split(','):
            s3_olci_bands.append(band)

s3_olci_bands = list(set(s3_olci_bands))


# Import Sentinel-3 OLCI product

operators = [
    'Read', 
    'Idepix.Sentinel3.Olci',
    'Reproject',
    'Write'
]

read = dict()
read['file'] = s3_file

idepix = get_operator_default_parameters('Idepix.Sentinel3.Olci')
idepix['reflBandsToCopy'] = ','.join(s3_olci_bands)

reproject = dict()
reproject['crs'] = 'EPSG:4326'

write = dict()
write['file'] = 's3_olci'

s3_olci_import(idepix,
               Read=read, 
               Reproject=reproject, 
               Write=write)

# RGB Composites

date_format = '%Y%m%dT%H%m%S'

output_startdate = input_metadata.iloc[0]['startdate']
output_stopdate = input_metadata.iloc[0]['enddate']


for k, v in composites.iteritems():
    
    print("k = {0}".format(k))
    
    bands = [os.path.join(write['file'] + '.data', '{}.img'.format(band)) for band in (composites[k]['bands'].split(',') + ['pixel_classif_flags'])]
    
    print("BANDS = {0}".format(bands))
    
    ds = gdal.Open(bands[0])

    geo_transform = ds.GetGeoTransform()
    projection_ref = ds.GetProjectionRef()
    
    ds = None
    
    s3_rgb_data = read_s3(bands)
    
    red = s3_rgb_data[:,:,0]
    green = s3_rgb_data[:,:,1]
    blue = s3_rgb_data[:,:,2]
    classif_flags = s3_rgb_data[:,:,3]
    
    date_format = '%Y%m%dT%H%m%S'
    
    output_name = '-'.join(
        [
            k.replace(' ', '-').upper(),
            output_startdate.strftime(date_format),
            output_startdate.strftime(date_format)
        ]
    )
    
    s3_rgb_composite(
        red, 
        green,
        blue, 
        classif_flags,
        geo_transform,
        projection_ref, 
        output_name + '.tif'
    )
    
    date_format = '%Y-%m-%dT%H:%m:%S'
    
    with open(output_name + '.tif.properties', 'wb') as file:
        file.write('title={} ({}/{})\n'.format(k, output_startdate.strftime(date_format), output_stopdate.strftime(date_format)))
        file.write('date={}Z/{}Z\n'.format(output_startdate.strftime(date_format), output_stopdate.strftime(date_format)))   
        file.write('geometry={}'.format(input_metadata.iloc[0].wkt))

    # PNG
    gdal.Translate('{}.png'.format(output_name), '{}.tif'.format(output_name), format='PNG')

    os.remove('{}.png.aux.xml'.format(output_name))

    with open(output_name + '.png.properties', 'wb') as file:
        file.write('title={} - Quicklook ({}/{})\n'.format(k, output_startdate.strftime(date_format), output_stopdate.strftime(date_format)))
        file.write('date={}Z/{}Z\n'.format(output_startdate.strftime(date_format), output_stopdate.strftime(date_format)))   
        file.write('geometry={}'.format(input_metadata.iloc[0].wkt))


# Clean-up

shutil.rmtree('{}.data'.format(write['file']))

os.remove('{}.dim'.format(write['file']))