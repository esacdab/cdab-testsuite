#!/opt/anaconda/envs/env_s3/bin/python
##########################
## This python files Read command-line arguments
## Usage: s3_olci_mosaic.py <input_product_dir> <cloud_coverage_threshold> <output_dir>
## cloud_coverage_threshold should be value in [0,1]
##########################
from __future__ import print_function
import os
import sys
import shutil
os.environ['PREFIX'] = '/opt/anaconda/envs/env_s3/'
os.environ['GPT_BIN'] = os.path.join(os.environ['PREFIX'], 'snap/bin/gpt')

os.environ['GDAL_DATA'] =  os.path.join(os.environ['PREFIX'], 'share/gdal')
os.environ['PROJ_LIB'] = os.path.join(os.environ['PREFIX'], 'share/proj')

sys.path.append('/opt/anaconda/envs/env_s3/snap/.snap/snap-python')
from snap_util_helpers import *
import gdal
import math 
import numpy as np
gdal.UseExceptions()
###########################
input_dir = sys.argv[1]
cloud_coverage_threshold = float(sys.argv[2])
if len(sys.argv) >= 4:
    output_dir = sys.argv[3]
else:
    output_dir = os.getcwd()
##########################
os.chdir(output_dir)
s3_prod_list = os.listdir(input_dir)

s3_prod_path=[]
for input_prod in s3_prod_list:
    for dirName, subdirList, fileList in os.walk(os.path.join(input_dir,input_prod)):
        if 'xfdumanifest.xml' in fileList :
            print(input_prod+ ' found', file=sys.stderr)
            s3_prod_path.append(os.path.join(dirName,'xfdumanifest.xml'))
            

            
ndvi_lambda = lambda x,y,w,v: 255 if(x+y)==0 or w or v==0 else  (x-y)/float(x+y)
vfunc_ndvi = np.vectorize(ndvi_lambda, otypes=[np.float])

driver = gdal.GetDriverByName('GTiff')    

####Pre-Process Inputs
input_tif_list=[]
pre_proc_inputs=[]
for s3_id in s3_prod_path:
    mygraph =GraphProcessor(os.environ['GPT_BIN'])

    # Read

    operator = 'Read'
    source_node_id = ''
    parameters = get_operator_default_parameters(operator)

    node_id = 'Read'
    parameters['formatName'] = 'Sen3'    
    parameters['file'] = s3_id

    mygraph.add_node(node_id, operator, parameters, source_node_id)

    # TOPSAR-Split
    operator = 'Reproject'

    source_node_id = node_id

    node_id = 'Reproject'

    parameters = get_operator_default_parameters(operator)
    parameters['crs'] = 'EPSG:4326'

    mygraph.add_node(node_id, operator, parameters, source_node_id)

    # Write
    operator = 'Write'

    source_node_id = node_id

    node_id = 'Write'
    dim_name = os.path.dirname(s3_id).split('/')[-1].split('.SEN3')[0]
    parameters = get_operator_default_parameters(operator)
    parameters['file'] = dim_name
    

    mygraph.add_node(node_id, operator, parameters, source_node_id)


    mygraph.save_graph('graph_{}.xml'.format(dim_name))

    ####RUN graph 
    mygraph.run()
    if not os.path.isfile(os.path.join(output_dir,'{}.dim'.format(dim_name))):
        print('{} dropped preprocessing!'.format(dim_name), file=sys.stderr)
    else: 
        print('{} been sucessfully preprocessed!'.format(dim_name), file=sys.stderr)
        pre_proc_inputs.append(dim_name)
        input_id = dim_name
        print('Initiate gdal processing of: '+ input_id, file=sys.stderr)
        bands = [os.path.join(output_dir,'{}.data'.format(input_id), '{}.img'.format(band)) for band in ['RC681', 'RC865', 'LQSF']]

        ds = gdal.Open(bands[0])

        geo_transform = ds.GetGeoTransform()
        projection_ref = ds.GetProjectionRef()
        ds = None
        s3_data = read_s3(bands)
        red = s3_data[:,:,0]
        nir = s3_data[:,:,1]
        lqsf = s3_data[:,:,2]
        cloud_mask =  get_mask('CLOUD', lqsf)
        mask = get_mask('LAND', lqsf)
        cloud_percentage = (cloud_mask==1).sum()/cloud_mask.size
        print('cloud percentage for {} is {}'.format(input_id,cloud_percentage), file=sys.stderr)

        if cloud_percentage >= cloud_coverage_threshold:
            print('{} does exceed cloud threshod, therefore discarded!'.format(input_id), file=sys.stderr)

        else:
            ndvi=vfunc_ndvi(nir, red, cloud_mask , mask)
            output = driver.Create('tmp_{}.tif'.format(input_id), 
                   red.shape[1], 
                   red.shape[0], 
                   1, 
                   gdal.GDT_Float32)

            output.SetGeoTransform(geo_transform)
            output.SetProjection(projection_ref)
            output.GetRasterBand(1).WriteArray(ndvi)

            output.FlushCache()

            output = None
            if os.path.isfile(os.path.join(output_dir,'tmp_{}.tif'.format(input_id))):
                print('tmp_{}.tif been sucessfully generated!'.format(input_id), file=sys.stderr)
                input_tif_list.append(os.path.join(output_dir,'tmp_{}.tif'.format(input_id)))
                os.remove(os.path.join(output_dir,'{}.dim'.format(input_id)))
                shutil.rmtree(os.path.join(output_dir,'{}.data'.format(input_id)))  
            else:
                print('tmp_{}.tif been failed generating!'.format(input_id), file=sys.stderr)



        
##Attention for noData value if you change the band
vrt_options = gdal.BuildVRTOptions(resampleAlg='average',srcNodata = 255,VRTNodata = 255, addAlpha=False)
my_vrt = gdal.BuildVRT(os.path.join(output_dir,'my.vrt'), input_tif_list, options=vrt_options)
my_vrt = None


translate_options = gdal.TranslateOptions(gdal.ParseCommandLine("-co TILED=YES -co COPY_SRC_OVERVIEWS=YES -co COMPRESS=LZW -co BIGTIFF=YES -ot Float32"))
ds = gdal.Open(os.path.join(output_dir,'my.vrt'))
gdal.SetConfigOption('COMPRESS_OVERVIEW', 'DEFLATE')
ds.BuildOverviews('NEAREST', [2,4,8,16,32])

ds = gdal.Translate(os.path.join(output_dir,'mosaic.tif'), ds, options=translate_options)
ds = None


if os.path.isfile(os.path.join(output_dir,'mosaic.tif')):
    print('output generated successfully by mosaicing {} S3 OLCI products.'.format(len(input_tif_list)), file=sys.stderr)

    if create_rgba(os.path.join(output_dir,'mosaic.tif'), os.path.join(output_dir,'ndvi_rgba.tif')):
        print('rgb-a output is also generated successfully.', file=sys.stderr)
        
        
try:
    os.remove(os.path.join(output_dir,'my.vrt.ovr'))
    os.remove(os.path.join(output_dir,'my.vrt'))
    for input_tif in input_tif_list:
              os.remove(input_tif)
              
    for leftover_inputs in os.listdir(output_dir):

        suffix=os.path.basename(leftover_inputs).split('.')[-1]

        if suffix=='data':
            shutil.rmtree(os.path.join(output_dir,leftover_inputs))
        elif suffix=='dim' or suffix=='xml':
            os.remove(os.path.join(output_dir,leftover_inputs))
        
              
except:
    pass   
