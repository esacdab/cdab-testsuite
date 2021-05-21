import lxml.etree as etree
import subprocess
import tempfile
import time
import math 
import os
import sys
sys.path.append('/opt/anaconda/envs/env_s3/snap/.snap/snap-python')
import snappy 
from snappy import GPF
import logging
import gdal
import numpy as np
logging.basicConfig(stream=sys.stderr, 
                    level=logging.INFO,
                    format='%(asctime)s %(levelname)-8s %(message)s',
                    datefmt='%Y-%m-%dT%H:%M:%S')

from pygments import highlight
from pygments.lexers import XmlLexer
from pygments.formatters import HtmlFormatter
import IPython
from IPython.display import HTML

def display_xml_nice(xml):
    formatter = HtmlFormatter()
    IPython.display.display(HTML('<style type="text/css">{}</style>    {}'.format(formatter.get_style_defs('.highlight'), highlight(xml, XmlLexer(), formatter))))


def run_command(command, **kwargs):
    
    process = subprocess.Popen(args=command, stdout=subprocess.PIPE, **kwargs)
    while True:
        output = process.stdout.readline()
        if output.decode() == '' and process.poll() is not None:
            break
        if output:
            logging.info(output.strip().decode())
    rc = process.poll()
    return rc
    
class GraphProcessor():
    """SNAP Graph class

    This class provides the methods to create, view and run a SNAP Graph

    Attributes:
        None.
    """
    
    def __init__(self, gpt_path, wdir='.'):
        self.root = etree.Element('graph')
    
        version = etree.SubElement(self.root, 'version')
        version.text = '1.0'
        self.pid = None
        self.p = None
        self.wdir = wdir
        self.gpt_path = gpt_path

    def view_graph(self):
        """This method prints SNAP Graph
    
        Args:
            None.

        Returns
            None.

        Raises:
            None.
        """
        
        display_xml_nice(etree.tostring(self.root , pretty_print=True))
        #print(etree.tostring(self.root , pretty_print=True))
        
    def add_node(self, node_id, operator, parameters, source):
        """This method adds or overwrites a node to the SNAP Graph
    
        Args:
            node_id: node identifier
            operator: SNAP operator
            parameter: dictionary with the SNAP operator parameters
            source: string or list of sources (previous node identifiers in the SNAP Graph)

        Returns
            None.

        Raises:
            None.
        """
        xpath_expr = '/graph/node[@id="%s"]' % node_id

        if len(self.root.xpath(xpath_expr)) != 0:

            node_elem = self.root.xpath(xpath_expr)[0]
            operator_elem = self.root.xpath(xpath_expr + '/operator')[0]
            sources_elem = self.root.xpath(xpath_expr + '/sources')[0]
            parameters_elem = self.root.xpath(xpath_expr + '/parameters')

            for key, value in parameters.iteritems():
                
                if key == 'targetBandDescriptors':
                                        
                    parameters_elem.append(etree.fromstring(value))
                    
                else:
                    p_elem = self.root.xpath(xpath_expr + '/parameters/%s' % key)[0]

                    if value is not None:             
                        if value[0] != '<':
                            p_elem.text = value
                        else:
                            p_elem.text.append(etree.fromstring(value))
    
        else:

            node_elem = etree.SubElement(self.root, 'node')
            operator_elem = etree.SubElement(node_elem, 'operator')
            sources_elem = etree.SubElement(node_elem, 'sources')

            if isinstance(source, list):

                for index, s in enumerate(source):
                    if index == 0:  
                        source_product_elem = etree.SubElement(sources_elem, 'sourceProduct')

                    else: 
                        source_product_elem = etree.SubElement(sources_elem, 'sourceProduct.%s' % str(index))

                    source_product_elem.attrib['refid'] = s
            
            elif isinstance(source, dict):

                for key, value in source.iteritems():
                    
                    source_product_elem = etree.SubElement(sources_elem, key)
                    source_product_elem.text = value
            
            elif source != '':
                source_product_elem = etree.SubElement(sources_elem, 'sourceProduct')
                source_product_elem.attrib['refid'] = source

            parameters_elem = etree.SubElement(node_elem, 'parameters')
            parameters_elem.attrib['class'] = 'com.bc.ceres.binding.dom.XppDomElement'

            for key, value in parameters.items():

                if key == 'targetBandDescriptors':
                                        
                    parameters_elem.append(etree.fromstring(value))
                    
                else:
                
                    parameter_elem = etree.SubElement(parameters_elem, key)

                    if value is not None:             
                        if value[0] != '<':
                            parameter_elem.text = value
                        else:
                            parameter_elem.append(etree.fromstring(value))

        node_elem.attrib['id'] = node_id

        operator_elem.text = operator 

    def save_graph(self, filename):
        """This method saves the SNAP Graph
    
        Args:
            filename: XML filename with '.xml' extension

        Returns
            None.

        Raises:
            None.
        """
        with open(filename, 'w') as file:
            file.write('<?xml version="1.0" encoding="UTF-8"?>\n')
            file.write(etree.tostring(self.root, pretty_print=True).decode())
     

    def run(self):
        """This method runs the SNAP Graph using gpt
    
        Args:
            None.

        Returns
            res: gpt exit code 
            err: gpt stderr

        Raises:
            None.
        """
        os.environ['LD_LIBRARY_PATH'] = '.'
        os.environ['GPT_BIN'] = '/opt/anaconda/envs/env_s3/snap/bin/gpt'
        
        logging.info('Processing the graph')
        
        fd, path = tempfile.mkstemp()
        
        rc = 1
        try:
        
            self.save_graph(filename=path)

            options = [self.gpt_path,
               '-x',
               '-c',
               '2048M',
               path]
            rc = run_command(options)
            
        except Exception as e:
            logging.info('Error:{}'.format(e))
            
            
            
        finally:
            os.remove(path)
            
        logging.info('Done.')
        return rc


        
def get_snap_parameters(operator):
    """This function returns the SNAP operator ParameterDescriptors (snappy method op_spi.getOperatorDescriptor().getParameterDescriptors())
    
    Args:
        operator: SNAP operator
        
    Returns
        The snappy object returned by op_spi.getOperatorDescriptor().getParameterDescriptors().
    
    Raises:
        None.
    """
    op_spi = GPF.getDefaultInstance().getOperatorSpiRegistry().getOperatorSpi(operator)

    op_params = op_spi.getOperatorDescriptor().getParameterDescriptors()

    return op_params

def get_operator_default_parameters(operator):
    """This function returns a Python dictionary with the SNAP operator parameters and their default values, if available.
    
    Args:
        operator: SNAP operator
        
    Returns
        A Python dictionary with the SNAP operator parameters and their default values.
    
    Raises:
        None.
    """
    parameters = dict()

    for param in get_snap_parameters(operator):
    
        parameters[param.getName()] = param.getDefaultValue()
    
    return parameters

def get_operator_help(operator):
    """This function prints the human readable information about a SNAP operator 
    
    Args:
        operator: SNAP operator
        
    Returns
        The human readable information about the provided SNAP operator.
    
    Raises:
        None.
    """
    op_spi = GPF.getDefaultInstance().getOperatorSpiRegistry().getOperatorSpi(operator)

    logging.info('Operator name: {}'.format(op_spi.getOperatorDescriptor().getName()))

    logging.info('Operator alias: {}\n'.format(op_spi.getOperatorDescriptor().getAlias()))
    logging.info('Parameters:\n')
    param_Desc = op_spi.getOperatorDescriptor().getParameterDescriptors()

    for param in param_Desc:
        logging.info('{}: {}\nDefault Value: {}\n'.format(param.getName(),
                                                   param.getDescription(),
                                                   param.getDefaultValue()))

        logging.info('Possible values: {}\n').format(list(param.getValueSet()))
            
            



    
def read_s3(bands):

    gdal.UseExceptions()
    
    stack = []
    
    for index, band in enumerate(bands):
        
        temp_ds = gdal.Open(band) 
 
        if not temp_ds:
            raise ValueError()
            
        stack.append(temp_ds.GetRasterBand(1).ReadAsArray())
      
    return np.dstack(stack)



def get_mask(flag, lqsf):
    
    pixel_classif_flags = {'INVALID': 1,
                             'WATER': 2,
                             'LAND': 4,
                             'CLOUD': 8,
                             'CLOUD_AMBIGUOUS': 8388608,
                             'CLOUD_MARGIN': 16777216,
                             'SNOW_ICE': 16,
                             'INLAND_WATER': 32,
                             'TIDAL': 64,
                             'COSMETIC': 128,
                             'SUSPECT': 256, 
                          'HISOLZEN': 512,
                          'SATURATED': 1024,
                          'WV_FAIL': 2048,
                          'OGVI_FAIL': 4096,
                          'OTCI_FAIL': 8192,
                          'LRAYFAIL': 16384,
                          'OGVI_CLASS_BAD': 32768,
                          'OGVI_CLASS_WS': 65536,
                          'OGVI_CLASS_CSI': 131072,
                          'OGVI_CLASS_BRIGHT': 262144,
                          'OGVI_CLASS_INVAL_REC': 524288,
                          'OTCI_BAD_IN': 1048576,
                          'OTCI_CLASS_ANG': 2097152,
                          'OTCI_CLASS_CLSN': 4194304
                          }
    
    
    b1 = int(math.log(pixel_classif_flags[flag], 2))
    b2 = b1
    
    return _capture_bits(lqsf.astype(np.int64), b1, b2)

def _capture_bits(arr, b1, b2):
    
    width_int = int((b1 - b2 + 1) * "1", 2)
 
    return ((arr >> b2) & width_int).astype('uint8')



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
    
    
    red_band[np.where(band<0.1)] = 255
    red_band[np.where(band >= 0.1)] = 217
    red_band[np.where(band >= 0.2)] = 102
    red_band[np.where(band >= 0.4)] = 32
    red_band[np.where(band >= 0.7)] = 19

    green_band[np.where(band<0.1)] = 255
    green_band[np.where(band >= 0.1)] = 255
    green_band[np.where(band >= 0.2)] = 153
    green_band[np.where(band >= 0.4)] = 128
    green_band[np.where(band >= 0.7)] = 77
    

    blue_band[np.where(band<0.1)] = 204
    blue_band[np.where(band >= 0.1)] = 179
    blue_band[np.where(band >= 0.2)] = 0
    
    alpha_band[np.where(band != 255)] = 255

    
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
