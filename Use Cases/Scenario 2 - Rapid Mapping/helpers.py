import os
import sys
sys.path.append(os.path.join(os.environ['PREFIX'], 'conda-otb/lib/python'))
os.environ['OTB_APPLICATION_PATH'] = os.path.join(os.environ['PREFIX'], 'conda-otb/lib/otb/applications')
os.environ['GDAL_DATA'] =  os.path.join(os.environ['PREFIX'], 'share/gdal')
os.environ['PROJ_LIB'] = os.path.join(os.environ['PREFIX'], 'share/proj')
os.environ['GPT_BIN'] = os.path.join(os.environ['PREFIX'], 'snap/bin/gpt')
import otbApplication

import gdal
import numpy as np
import math
from PIL import Image, ImageDraw
from struct import unpack
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



def contrast_enhancement(in_tif, out_tif, hfact=1.0):

    ContrastEnhancement = otbApplication.Registry.CreateApplication("ContrastEnhancement")

    ContrastEnhancement.SetParameterString("in", in_tif)
    ContrastEnhancement.SetParameterString("out", out_tif)
    ContrastEnhancement.SetParameterOutputImagePixelType("out", otbApplication.ImagePixelType_uint8)
    ContrastEnhancement.SetParameterFloat("nodata", 0.0)
    ContrastEnhancement.SetParameterFloat("hfact", hfact)
    ContrastEnhancement.SetParameterInt("bins", 256)
    ContrastEnhancement.SetParameterInt("spatial.local.w", 500)
    ContrastEnhancement.SetParameterInt("spatial.local.h", 500)
    ContrastEnhancement.SetParameterString("mode","lum")

    ContrastEnhancement.ExecuteAndWriteOutput()

    
def hot_spot(b8a_path, b12_path, output_name):
    
    gain = 10000
    
    ds_b8a = gdal.Open(b8a_path, gdal.GA_ReadOnly)
    ds_b12 = gdal.Open(b12_path, gdal.GA_ReadOnly)
    
    b8A = ds_b8a.GetRasterBand(1).ReadAsArray()
    b12 = ds_b12.GetRasterBand(1).ReadAsArray()
    
    width = ds_b8a.RasterXSize
    height = ds_b8a.RasterYSize
    input_geotransform = ds_b8a.GetGeoTransform()
    input_georef = ds_b8a.GetProjectionRef()
    
    hot_spot = np.zeros((width,height),dtype=np.uint8)
    r = np.zeros((width,height))
    
    # Calculate ratio r and difference detla
    r[np.where(b8A > 0)] = b12[np.where(b8A > 0)] / b8A[np.where(b8A > 0)]
    delta = b12 - b8A

    b8A = None
    ds_b8a = None
    
    # Step 1 : mask obvious water pixels (value 3)
    # B12 < 0.04 are flagged as water and thus are excluded
    hot_spot[np.where(b12 < (0.04 * gain))] = 3

    # Step 2 : identify obvious fire pixels (value 1)
    hot_spot[np.where((hot_spot == 0) & (r > 2) & (delta > (0.15 * gain)))] = 1

    # Step 3 : identify candidate fire pixels (value 2)
    hot_spot[np.where((hot_spot == 0) & (r > 1.1) & (delta > (0.1 * gain)))] = 2

    # Step 4 : background characterization around candidate fire pixelscase of large fire.
    hot_spot[np.where(hot_spot == 3)] = 0
    
    
    for j in range(height):

        for i in range(width):

            # If the pixel is a candidate fire pixel (value = 2), we have to decide
            if hot_spot[i, j] == 2:

                # Find an appropriate size for a square window centered on the candidate fire pixel
                # default size is 91 x 91 pixels (1820m * 1820m)
                # We increase the size while the number of no obvious or candidate fire pixels is less than the half of total pixels in the window.
                d = 91
                i_ind1, i_ind2, j_ind1, j_ind2 = radius_index(i, j, d, width, height)
                nbr_pixels = math.floor(math.pow(d, 2) / 2)

                while np.size(np.where(hot_spot[i_ind1:i_ind2,j_ind1:j_ind2] == 0))/2 < nbr_pixels:
                    d += 8
                    i_ind1,i_ind2,j_ind1,j_ind2 = radius_index(i,j,d,width,height)
                    nbr_pixels = math.floor(math.pow(d,2) / 2)

                # background_characterization in the defined square window centered on the candidate fire pixel
                # Statistics are computed for pixels within the background : mean and stdv of r; 
                # mean and stdv of B12
                r_m =  np.mean(r[np.where(hot_spot[i_ind1:i_ind2,j_ind1:j_ind2] == 0)])
                r_std = np.std(r[np.where(hot_spot[i_ind1:i_ind2,j_ind1:j_ind2] == 0)])

                B12_m = np.mean(b12[np.where(hot_spot[i_ind1:i_ind2,j_ind1:j_ind2] == 0)])
                B12_std = np.std(b12[np.where(hot_spot[i_ind1:i_ind2,j_ind1:j_ind2] == 0)])

                # Step 5 : Contextual tests
                # Here we decide for all candidate fire pixels (value 2) if they are fire (value 1) or not (value 0)
                # Two conditions have to be sattisfied to flag a candidate pixel as fire pixel
                if ( r[i,j] > r_m + max((3 * r_std),(0.5 * gain)) ) and ( b12[i,j] > b12_m + max((3 * b12_std),(0.05 * gain)) ):
                    hot_spot[i,j] = 1
                else:
                    hot_spot[i,j] = 0
     
    b12 = None
    ds_b12 = None
    
    driver = gdal.GetDriverByName('GTiff')
    
    output = driver.Create(output_name, 
                           width, 
                           height, 
                           1, 
                           gdal.GDT_Byte)
    
    
    output.SetGeoTransform(input_geotransform)
    output.SetProjection(input_georef)
    output.GetRasterBand(1).WriteArray(hot_spot)

    output.FlushCache()
    
    hot_spot = None
    delta = None
    
    return True


def radius_index(i, j, d, width, height):
    
    i_ind1 = i - d
    i_ind2 = i + d + 1
    j_ind1 = j - d
    j_ind2 = j + d + 1
    
    if i_ind1 < 0:
        i_ind1 = 0
    
    if i_ind2 >= width:
        i_ind2 = width-1
    
    if j_ind1 < 0:
        j_ind1 = 0
    
    if j_ind2 >= height:
        j_ind2 = height-1

    return i_ind1, i_ind2, j_ind1, j_ind2


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
    
    mygraph = GraphProcessor('/opt/anaconda/envs/env_ewf_burned_area/snap/bin/gpt')
    
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
    
    mygraph = GraphProcessor('/opt/anaconda/envs/env_ewf_burned_area/snap/bin/gpt')
    
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
    
    
def raster2rgb(raster_file, color_table, out_file_name, raster_band=1, discrete=True):
    
    #Reading the band
    data_types ={'Byte':'B','UInt16':'H','Int16':'h','UInt32':'I','Int32':'i','Float32':'f','Float64':'d'}
    if os.path.isfile(raster_file) is False:
            raise Exception('[Errno 2] No such file or directory: \'' + raster_file + '\'')    
    
    dataset = gdal.Open(raster_file, gdal.GA_ReadOnly)
    
    if dataset == None:
        raise Exception("Unable to read the data file")
        
    geoTransform = dataset.GetGeoTransform()
    proj = dataset.GetProjection()
    
    band = dataset.GetRasterBand(raster_band)
    values = band.ReadRaster( 0, 0, band.XSize, band.YSize, band.XSize, band.YSize, band.DataType )
    values = unpack(data_types[gdal.GetDataTypeName(band.DataType)]*band.XSize*band.YSize,values)
    
    #Preparing the color table and the output file
    classification_values = color_table.keys()
    classification_values = sorted(classification_values)
    
    base = Image.new( 'RGBA', (band.XSize,band.YSize) )
    base_draw = ImageDraw.Draw(base)
    alpha_mask = Image.new('L', (band.XSize,band.YSize), 255)
    alpha_draw = ImageDraw.Draw(alpha_mask)
    
    #Reading the value and setting the output color for each pixel
    for pos in range(len(values)):
        y = pos/band.XSize
        x = pos - y * band.XSize
        for index in range(len(classification_values)):

            if values[pos] <= classification_values[index] or index == len(classification_values)-1:
                if discrete == True:
                    if index == 0:
                        index = 1
                    elif index == len(classification_values)-1 and values[pos] >= classification_values[index]:
                        index = index + 1
                    color = color_table[classification_values[index-1]]
                    base_draw.point((x,y), (color[0],color[1],color[2]))
                    alpha_draw.point((x,y),color[3])
                else:
                    if index == 0:
                        r = color_table[classification_values[0]][0]
                        g = color_table[classification_values[0]][1]
                        b = color_table[classification_values[0]][2]
                        a = color_table[classification_values[0]][3]
                    elif index == len(classification_values)-1 and values[pos] >= classification_values[index]:
                        r = color_table[classification_values[index]][0]
                        g = color_table[classification_values[index]][1]
                        b = color_table[classification_values[index]][2]
                        a = color_table[classification_values[index]][3]
                    else:
                        r = color_table[classification_values[index-1]][0] + (values[pos] - classification_values[index-1])*(color_table[classification_values[index]][0] - color_table[classification_values[index-1]][0])/(classification_values[index]-classification_values[index-1]) 
                        g = color_table[classification_values[index-1]][1] + (values[pos] - classification_values[index-1])*(color_table[classification_values[index]][1] - color_table[classification_values[index-1]][1])/(classification_values[index]-classification_values[index-1]) 
                        b = color_table[classification_values[index-1]][2] + (values[pos] - classification_values[index-1])*(color_table[classification_values[index]][2] - color_table[classification_values[index-1]][2])/(classification_values[index]-classification_values[index-1]) 
                        a = color_table[classification_values[index-1]][3] + (values[pos] - classification_values[index-1])*(color_table[classification_values[index]][3] - color_table[classification_values[index-1]][3])/(classification_values[index]-classification_values[index-1]) 
                    
                    base_draw.point((x,y), (int(r),int(g),int(b)))
                    alpha_draw.point((x,y),int(a))
                    
                break
    #Adding transparency and saving the output image       
    color_layer = Image.new('RGBA', base.size, (255, 255, 255, 0))
    base = Image.composite(color_layer, base, alpha_mask)
    base.save(out_file_name)

    # update geolocation
    ds_rgb = gdal.Open(out_file_name,1)
    ds_rgb.SetGeoTransform(geoTransform)
    ds_rgb.SetProjection(proj)
    
    ds_rgb.FlushCache()
    
    ds_rgb = None

    
