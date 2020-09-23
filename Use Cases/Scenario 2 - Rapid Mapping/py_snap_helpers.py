import lxml.etree as etree
import subprocess
import tempfile
import time
import os
import sys
import snappy 
from snappy import GPF
import logging

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
    
    def __init__(self, gpt_path='/opt/anaconda/envs/env_s3/snap/bin/gpt', wdir='.'):
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
        
        logging.info('Processing the graph')
        
        fd, path = tempfile.mkstemp()
        
        rc = None
        
        try:
        
            self.save_graph(filename=path)

            options = [self.gpt_path,
               '-x',
               '-c',
               '1024M',
               path]

            rc = run_command(options)

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
            
            
    
def op_help(op):
    """This function prints the human readable information about a SNAP operator 
    
    Args:
        op: the SNAP operator 
        
    Returns
        Human readable information about a SNAP operator.
    
    Raises:
        None.
    """
    op_spi = snappy.GPF.getDefaultInstance().getOperatorSpiRegistry().getOperatorSpi(op)

    logging.info('Operator name: {}'.format(op_spi.getOperatorDescriptor().getName()))

    logging.info('Operator alias: {}\n'.format(op_spi.getOperatorDescriptor().getAlias()))
    logging.info('Parameters:\n')
    param_Desc = op_spi.getOperatorDescriptor().getParameterDescriptors()

    for param in param_Desc:
        logging.info('{}: {}\nDefault Value: {}\n'.format(param.getName(),
                                                   param.getDescription(),
                                                   param.getDefaultValue()))

        logging.info('Possible values: {}\n').format(list(param.getValueSet()))

def get_operators():
    """This function provides a Python dictionary with all SNAP operators. 
    
    Args:
        None.
        
    Returns
        Python dictionary with all SNAP operators.
    
    Raises:
        None.
    """
    snappy.GPF.getDefaultInstance().getOperatorSpiRegistry().loadOperatorSpis()

    op_spi_it = snappy.GPF.getDefaultInstance().getOperatorSpiRegistry().getOperatorSpis().iterator()

    snap_operators = dict()

    while op_spi_it.hasNext():

        op_spi = op_spi_it.next()

        op_class = op_spi.getOperatorDescriptor().getName()

        if 's1tbx' in op_spi.getOperatorDescriptor().getName():

            op_toolbox = 's1tbx'

        elif 's2tbx' in op_spi.getOperatorDescriptor().getName():

            op_toolbox = 's2tbx'

        elif 's3tbx' in op_spi.getOperatorDescriptor().getName():

            op_toolbox = 's3tbx'
        else:

            op_toolbox = 'other'

        snap_operators[op_spi.getOperatorAlias()] = {'name' : op_spi.getOperatorDescriptor().getName(), 
                                                     'toolbox' : op_toolbox}
        
    return snap_operators

def get_write_formats():
    """This function provides a human readable list of SNAP Write operator formats. 
    
    Args:
        None.
        
    Returns
        Human readable list of SNAP Write operator formats.
    
    Raises:
        None.
    """
    ProductIOPlugInManager = snappy.jpy.get_type('org.esa.snap.core.dataio.ProductIOPlugInManager')

    ProductWriterPlugIn = snappy.jpy.get_type('org.esa.snap.core.dataio.ProductWriterPlugIn')

    write_plugins = ProductIOPlugInManager.getInstance().getAllWriterPlugIns()

    while write_plugins.hasNext():
        plugin = write_plugins.next()
        print ('{} ({})'.format(plugin.getFormatNames()[0], plugin.getDefaultFileExtensions()[0]))
        
        
        
def snap_graph(operators, **kwargs):
   
    options = dict()
   
    # deal with the SNAP operators with dots in their name
    op_name_exceptions = dict()

    for key in [k for k in get_operators().keys() if '.' in k]:

        op_name_exceptions[key.replace('.', '')] = key
    
    
    for operator in operators:
            
        parameters = get_operator_default_parameters(operator)
        
        options[operator] = parameters

    for key, value in kwargs.items():
        
        
        # deal with the SNAP operators with dots in their name
        if key in op_name_exceptions.keys():
            options[op_name_exceptions[key]].update(value)
        else:
            options[key.replace('_', '-')].update(value)
     
    mygraph = GraphProcessor()
    
    for index, operator in enumerate(operators):
    
        if index == 0:  
            
            source_node_id = ''
        
        else:
            source_node_id = operators[index - 1]
       
        # deal with the SNAP operators with dots in their name
        if operator in op_name_exceptions.keys():
            
            operator = op_name_exceptions[operator]
            
            mygraph.add_node(operator,
                             operator, 
                             idepix, source_node_id)
        else:
            mygraph.add_node(operator,
                             operator, 
                             options[operator], source_node_id)
    
    mygraph.view_graph()
    
    res = mygraph.run()
    
    return res

