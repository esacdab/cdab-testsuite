#!/opt/anaconda/envs/env_s3/bin/python
##########################
## This python files Read command-line arguments
## Usage: s3_slstr_lst.py <input_product_dir> <output_dir>
##########################
import os
import sys
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

if len(sys.argv) >= 3:
    output_dir = sys.argv[2]
else:
    output_dir = os.getcwd()
##########################

os.chdir(output_dir)
s3_prod_list = os.listdir(input_dir)

s3_prod_path = []
for input_prod in s3_prod_list:
    for dirName, subdirList, fileList in os.walk(os.path.join(input_dir, input_prod)):
        if 'xfdumanifest.xml' in fileList :
            print(input_prod+ ' found')
            s3_prod_path.append(os.path.join(dirName,'xfdumanifest.xml'))



lst_lambda = lambda x, y, z: 0 if y or z==0 or x<0 else round(x, 0)
vfunc_lst = np.vectorize(lst_lambda, otypes=[np.uint16])

driver = gdal.GetDriverByName('GTiff')
####Pre-Process Inputs

pre_proc_inputs=[]
input_tif_list=[]
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

    #### RUN graph
    mygraph.run()
    if not os.path.isfile(os.path.join(output_dir, '{}.dim'.format(dim_name))):
        print('{} dropped preprocessing!'.format(dim_name))
    else:
        print('{} been sucessfully preprocessed!'.format(dim_name))
        pre_proc_inputs.append(dim_name)
        input_id = dim_name
        print('Initiate gdal processing of: '+ input_id)
        bands = [os.path.join(output_dir,'{}.data'.format(input_id), '{}.img'.format(band)) for band in ['LST' , 'cloud_in', 'confidence_in']]

        ds = gdal.Open(bands[0])

        geo_transform = ds.GetGeoTransform()
        projection_ref = ds.GetProjectionRef()
        ds = None
        s3_data = read_s3(bands)
        lst = s3_data[:,:,0]
        cloud = s3_data[:,:,1]
        confidence = s3_data[:,:,2]

        mask = get_slstr_confidence_mask('land', confidence)
        cloud_mask =  get_slstr_mask('gross_cloud', cloud)
        lst_trimmed = vfunc_lst(lst, cloud_mask , mask)
        output = driver.Create(
            'tmp_{}.tif'.format(input_id),
            lst.shape[1],
            lst.shape[0],
            1,
            gdal.GDT_UInt16
        )

        output.SetGeoTransform(geo_transform)
        output.SetProjection(projection_ref)
        output.GetRasterBand(1).WriteArray(lst_trimmed)
        output.GetRasterBand(1).SetNoDataValue(0) #noData set as zero
        output.FlushCache()

        output = None
        if os.path.isfile(os.path.join(output_dir, 'tmp_{}.tif'.format(input_id))):
            print('tmp_{}.tif been sucessfully generated!'.format(input_id))
            input_tif_list.append(os.path.join(output_dir, 'tmp_{}.tif'.format(input_id)))

            vrt_options = gdal.BuildVRTOptions(resampleAlg='nearest', srcNodata = 0, VRTNodata = 0, addAlpha=False)  #noData set as zero
            my_vrt = gdal.BuildVRT(os.path.join(output_dir, 'my.vrt'), input_tif_list, options=vrt_options)
            my_vrt = None


            translate_options = gdal.TranslateOptions(gdal.ParseCommandLine("-co TILED=YES -co COPY_SRC_OVERVIEWS=YES -co COMPRESS=LZW -co BIGTIFF=YES"))
            ds = gdal.Open(os.path.join(output_dir, 'my.vrt'))
            gdal.SetConfigOption('COMPRESS_OVERVIEW', 'DEFLATE')
            ds.BuildOverviews('NEAREST', [2,4,8,16,32])

            ds = gdal.Translate(os.path.join(output_dir, 'lst_{}.tif'.format(input_id)), ds, options=translate_options)
            ds = None
            os.remove(os.path.join(output_dir, 'tmp_{}.tif'.format(input_id)))
            if create_rgba(os.path.join(output_dir, 'lst_{}.tif'.format(input_id)), os.path.join(output_dir, 'rgba_{}.tif'.format(input_id))):
                print('rgb-a output is also generated successfully.')

        else:
            print('tmp_{}.tif been failed generating!'.format(input_id))

try:
    os.remove(os.path.join(output_dir, 'my.vrt.ovr'))
    os.remove(os.path.join(output_dir, 'my.vrt'))

    for input_tif in input_tif_list:
        os.remove(input_tif)

    for leftover_inputs in os.listdir(output_dir):
        suffix = os.path.basename(leftover_inputs).split('.')[-1]

        if suffix == 'data':
            shutil.rmtree(os.path.join(output_dir, leftover_inputs))
        elif suffix == 'dim' or suffix == 'xml':
            os.remove(os.path.join(output_dir, leftover_inputs))

except:
    pass