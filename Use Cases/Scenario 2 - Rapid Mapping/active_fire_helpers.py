from py_snap_helpers import op_help, get_operator_default_parameters, GraphProcessor
import json
import os

def pre_processing(orbit_direction, **kwargs):
   
    options = dict()
    
    if orbit_direction == 'DESCENDING':

        operators = ['Read', 
                     'Rad2Refl',
                     'Resample',
                     'Reproject',
                     'Subset',
                     'Write']
    else:
        
        operators = ['Read', 
                 'Resample',
                 'Reproject',
                 'Subset',
                 'Write']
    
    for operator in operators:
            
        parameters = get_operator_default_parameters(operator)
        
        options[operator] = parameters

    for key, value in kwargs.items():
        
        options[key.replace('_', '-')].update(value)
    
    mygraph = GraphProcessor()
    
    for index, operator in enumerate(operators):
    
        if index == 0:            
            source_node_id = ''
        
        else:
            source_node_id = operators[index - 1]
        
        mygraph.add_node(operator,
                         operator, 
                         options[operator], source_node_id)
    
    
    mygraph.view_graph()
    
    mygraph.run()
    

def active_fire(**kwargs):
   
    options = dict()
    
    operators = ['Read', 
                 'BandMaths',
                 'Write']
    
    for operator in operators:
            
        parameters = get_operator_default_parameters(operator)
        
        options[operator] = parameters

    for key, value in kwargs.items():
        
        options[key.replace('_', '-')].update(value)
    
    mygraph = GraphProcessor()
    
    for index, operator in enumerate(operators):
    
        if index == 0:            
            source_node_id = ''
        
        else:
            source_node_id = operators[index - 1]
        
        mygraph.add_node(operator,
                         operator, 
                         options[operator], source_node_id)
    
    mygraph.view_graph()
    
    mygraph.run()

