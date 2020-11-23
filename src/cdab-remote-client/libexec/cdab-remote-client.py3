#!/usr/bin/env python

from cdab_shared import *
from connectors import openstack, google, amazon
import datetime
from enum import Enum
import io
import json
import netifaces as ni
import os
from os import path
import re
import socket
import sys
import threading
import time
import uuid
import xml.etree.ElementTree as ET
import yaml

class TestClient:
    """Main class for remote execution of the test scenarios TS11, TS12, TS13 and TS15.
    """

    VERSION = "1.30"

    errors = {
        ERR_CONFIG: 'Missing or invalid configuration',
        ERR_CREATE: 'Error creating the virtual machine',
        ERR_REMOTE: 'Error executing a command on the virtual machine',
        ERR_DELETE: 'Error deleting the virtual machine',
    }

    test_scenarios = {
        'TS11': {
            'test_scenario_description': 'Remote execution of catalogue access and download test on VM (single)',
            'test_case_name': 'TC411',
            'docker_image_id': 'esacdab/testsuite:latest',
            'docker_run_command': 'CDAB_CLIENT_DEFAULT',
            'cdab_client_test_scenario_id': 'TS11',
            'timeout': 1 * 60 * 60,
        },
        'TS12': {
            'test_scenario_description': 'Remote execution of catalogue access and download test on VM (multiple)',
            'test_case_name': 'TC412',
            'docker_image_id': 'esacdab/testsuite:latest',
            'docker_run_command': 'CDAB_CLIENT_DEFAULT',
            'cdab_client_test_scenario_id': 'TS12',
            'timeout': 1 * 60 * 60,
        },
        'TS13': {
            'test_scenario_description': 'Remote execution of processing test (multiple)',
            'test_case_name': 'TC413',
            'docker_image_id': 'esacdab/ewf-s3-olci-composites:0.41',
            'docker_run_command': 'PROCESSING',
            'test_target_url': 'https://catalog.terradue.com/sentinel3/search?uid=S3A_OL_1_EFR____20191110T230850_20191110T231150_20191112T030831_0179_051_215_3600_LN1_O_NT_002',
            'files': [ 's3-olci-composites.py' ],
            'timeout': 2 * 60 * 60,
        },
        'TS15.1': {
            'test_scenario_description': 'Remote execution of a predefined processing scenario test (NDVI)',
            'test_case_name': 'TC415',
            'docker_image_id': None,
            'docker_run_command': 'PROCESSING',
            'test_target_url': '',
            'tools': [ 'conda', 'opensearch-client', 'Stars' ],
            'timeout': 2 * 60 * 60,
        },
        'TS15.5': {
            'test_scenario_description': 'Remote execution of a predefined processing scenario test (interferogram)',
            'test_case_name': 'TC415',
            'docker_image_id': None,
            'docker_run_command': 'PROCESSING',
            'test_target_url': '',
            'tools': [ 'conda', 'opensearch-client', 'Stars' ],
            'timeout': 6 * 60 * 60,
        },
    }

    processing_scenarios = [
        'ndvi',
    ]

    target_site_uri_prefixes = {
        'CREO': 'https://auth.creodias.eu/',
        'MUNDI': 'https://mundiwebservices.com',
        'ONDA': 'https://catalogue.onda-dias.eu/',
        'SOBLOO': 'https://sobloo.eu/',
        'AMAZON': 'https://scihub.copernicus.eu/',
        'GOOGLE': 'https://scihub.copernicus.eu/',
    }

    target_site_s3_uri_prefixes = {
        'MUNDI': 'https://obs.eu-de.otc.t-systems.com/',
        'AMAZON': 'https://aws.amazon.com',
    }

    command_line = [
        { 'name': '-h', 'label': None, 'description': 'Display this help and exit' },
        { 'name': '-v', 'label': None, 'description': 'Display more information during processing' },
        { 'name': '-ml', 'label': None, 'description': 'Allow mixed log output (relevant for multiple parallel runs)' },
        { 'name': '-K', 'label': None }, # undocumented, for debugging purposes: keep virtual machine after test execution (do not delete it)
        { 'name': '-P', 'label': None }, # undocumented, for debugging purposes: do not hide passwords
        { 'name': '-X1', 'label': None }, # undocumented, for debugging purposes: exit before exeuting tests
        { 'name': '-conf', 'label': 'file', 'description': 'YAML file containing the remote configuration', 'default': '/opt/cdab-remote-client/etc/config.yaml' },
        { 'name': '-vm', 'label': 'number', 'type': 'int', 'description': 'Number of virtual machines to be run in parallel (min: 1)', 'default': 1 },
        { 'name': '-lf', 'label': 'number', 'type': 'int', 'description': 'Load factor (min: 1)', 'default': 1 },
        { 'name': '-sp', 'label': 'name', 'description': 'Service provider for test execution (as defined in configuration file)' },
        { 'name': '-ts', 'label': 'name', 'description': 'Target site for querying (as defined in configuration file)' },
        { 'name': '-te', 'label': 'url', 'description': 'Endpoint URL for remote target calls (overrides settings from target site set with -ts)', 'min_occurs': 0 },
        { 'name': '-tc', 'label': 'username:password', 'description': 'Credentials for target (overrides settings from target site set with -ts)', 'min_occurs': 0 },
        { 'name': '-psw', 'label': 'name', 'description': 'CWL workflow file (TS15 only) replacing default file', 'min_occurs': 0 },
        { 'name': '-psi', 'label': 'name', 'description': 'Text file with input product URLsfor workflow (TS15 only)', 'min_occurs': 0 },
        { 'name': '-i', 'label': 'name', 'description': 'Docker image identifier (URL)', 'default': None },
        { 'name': '-a', 'label': 'name', 'description': 'Docker authentication file (config.json)', 'default': None },
        { 'name': '-n', 'label': 'name', 'description': 'Test site name (parameter for cdab-client call)' },
        { 'name': None, 'label': 'test-scenario', 'description': 'Test scenario ID', 'possible_values': [s for s in test_scenarios] },
    ]

    compute_parameters = [
        { 'name': 'connector', 'description': "Connector type ('OpenStack', 'Google', 'Amazon')", 'default': 'OpenStack' },
        { 'name': 'auth_url', 'description': 'Authentication access point' },
        { 'name': 'username', 'description': 'Cloud username' },
        { 'name': 'password', 'description': 'Cloud password' },
        { 'name': 'account_file', 'description': 'JSON authentication file for Google service account' },
        { 'name': 'project_name', 'description': 'Project name' },
        { 'name': 'project_id', 'description': 'Project ID' },
        { 'name': 'user_domain_name', 'description': 'User domain name' },
        { 'name': 'region_name', 'description': 'Authentication region name' },
        { 'name': 'interface', 'description': 'Interface' },
        { 'name': 'identity_api_version', 'description': 'Volume API version' },
        { 'name': 'volume_api_version', 'description': 'Identity API version' },
        { 'name': 'vm_name', 'description': 'Preferred name of virtual machines to be created (sequential number is appended)' },
        { 'name': 'key_name', 'description': 'Name of predefined public key for SSH connection to new VM' },
        { 'name': 'image_name', 'description': 'Name of image to be used for new VM' },
        { 'name': 'flavor_name', 'list': True, 'description': 'Name of flavour for new VM' },
        { 'name': 'cost_monthly', 'list': True, 'type': 'float', 'description': 'Monthly cost for VM of specified flavour', 'default': 0 },
        { 'name': 'cost_hourly', 'list': True, 'type': 'float', 'description': 'Hourly cost of VM of specified flavour', 'default': 0 },
        { 'name': 'currency', 'description': 'Payment currency', 'default': 'EUR' },
        { 'name': 'network_name', 'list': True, 'description': 'Name of network to which new VM is connected' },
        { 'name': 'security_group', 'description': 'Name of security group for new VM' },
        { 'name': 'floating_ip', 'type': 'bool', 'description': 'Explicitly assign floating IP', 'default': False },
        { 'name': 'floating_ip_network', 'description': 'Network from which to assign floating IP' },
        { 'name': 'private_key_file', 'description': 'Location of the private key file for SSH connections to virtual machine (must correspond to public key in \'key_name\')' },
        { 'name': 'remote_user', 'description': 'User on virtual machine for SSH connections' },
        { 'name': 'use_volume', 'type': 'bool', 'description': 'Create an external volume for docker image and test execution', 'default': False },
        { 'name': 'use_tmp_volume', 'type': 'bool', 'description': 'Create an external volume for /tmp', 'default': False },
        { 'name': 'download_origin', 'description': 'Value of DOWNLOAD_ORIGIN environment variable for test execution on VM', 'default': "terradue" },
    ]

    stars_plugins = {
        "Plugins": {
            "Terradue": {
                "Assembly": "/usr/share/Stars-Terradue/Stars-Terradue.dll",
                "Suppliers": {
                    "ONDA": {
                        "Type": "Terradue.Data.Stars.Suppliers.DataHubSourceSupplier",
                        "Parameters": [
                            ""
                        ],
                        "ServiceUrl": "https://catalogue.onda-dias.eu/dias-catalogue"
                    },
                    "CREO": {
                        "Type": "Terradue.Data.Stars.Suppliers.DataHubSourceSupplier",
                        "Parameters": [
                            ""
                        ],
                        "ServiceUrl": "https://finder.creodias.eu/resto/api/collections/describe.xml"
                    },
                    "SOBLOO": {
                        "Type": "Terradue.Data.Stars.Suppliers.DataHubSourceSupplier",
                        "Parameters": [
                            ""
                        ],
                    "ServiceUrl": "https://sobloo.eu/api/v1/services/search"
                    },
                    "MUNDI": {
                        "Type": "Terradue.Data.Stars.Suppliers.DataHubSourceSupplier",
                        "Parameters": [
                            ""
                        ],
                        "ServiceUrl": "https://mundiwebservices.com/acdc/catalog/proxy/search"
                    },
                    "AMAZON": {
                        "Type": "Terradue.Data.Stars.Suppliers.DataHubSourceSupplier",
                        "Parameters": [
                            ""
                        ],
                        "ServiceUrl": "https://aws.amazon.com"
                    },
                    "GOOGLE": {
                        "Type": "Terradue.Data.Stars.Suppliers.DataHubSourceSupplier",
                        "ServiceUrl": "https://storage.googleapis.com",
                        "projectId": "ID",
                        "AccountFile": "AF"
                    }
                },
                "Translators": {
                    "AtomToStac": {
                        "Type": "Terradue.Data.Stars.Translators.AtomToStacTranslator",
                        "Parameters": [
                            ""
                        ]
                    }
                },
                "Processings": {
                    "KOMPSAT-3": {
                        "Type": "Terradue.Data.Stars.Model.Metadata.Kompsat3.Kompsat3MetadataExtraction"
                    },
                    "SENTINEL-1": {
                        "Type": "Terradue.Data.Stars.Model.Metadata.Sentinels.Sentinel1.Sentinel1MetadataExtractor"
                    }
                }
            }
        }
    }



    def __init__(self):

        Logger.verbose = False
        Logger.mixed_logs = False
        Logger.show_passwords = False
        TestClient.keep_vm = False
        TestClient.exit_at = None
        self.config_file = None
        self.service_provider_configs = None
        self.ca_certificates = []
        self.connect_retries = 30
        self.connect_interval = 0.5
        self.max_retention_hours = 6
        self.vm_count = None
        self.total_vm_count = None
        self.load_factor = None
        self.max_parallelism = None
        self.service_provider = None
        self.target_site = None
        self.target_endpoint = None
        self.target_credentials = None
        self.target_site_class = None
        self.target_site_uri_prefix = None
        self.target_site_s3_uri_prefix = None
        self.target_site_s3_key_id = None
        self.target_site_s3_secret_key = None
        self.docker_config = None
        self.docker_image_id = None
        self.docker_run_command = None
        self.test_site_name = None
        self.test_scenario_id = None
        self.test_scenario = None
        self.processing_scenario_id = None
        self.cdab_client_test_scenario_id = None
        self.test_scenario_description = None
        self.processing_scenario_cwl_file = None
        self.processing_scenario_input_file = None
        self.test_case_name = None
        self.test_target_url = None
        self.start_time = None
        self.end_time = None
        self.incomplete_deletion = False

        # Read and validate command-line arguments
        self.read_arguments()

        # Read test-site-related section in configuration file
        self.read_config_file()

        # Validate selected test scenario and set parameters
        self.check_scenario()

        self.connector = None




    def print_usage(message = None, exit = True, exit_code = 1):
        if message:
            print("ERROR: {0}\n".format(message), file=sys.stderr)

        print("cdab-remote-client version {0} (c) 2020 Terradue Srl.\n".format(TestClient.VERSION), file=sys.stderr)
        print("USAGE: {0} [OPTIONS]".format(sys.argv[0]), end="", file=sys.stderr)
        for cl in [r for r in TestClient.command_line if r['name'] is None]:
            print(" <{0}>".format(cl['label']), end="", file=sys.stderr)
        print("\n", file=sys.stderr)

        print("OPTIONS", file=sys.stderr)
        for cl in [r for r in TestClient.command_line if r['name'] and 'description' in r]:
            # Print option usage and help
            print("    {0}".format(TestClient.get_arg_str(cl)).ljust(30), end="", file=sys.stderr)
            print(cl['description'], file=sys.stderr)

            # Print possible values if exist for option
            if 'possible_values' in cl and cl['possible_values']:
                print("".ljust(30), end="", file=sys.stderr)
                print("Possible values: {0}".format(", ".join(cl['possible_values'])), file=sys.stderr)
            if 'default' in cl:
                print("".ljust(30), end="", file=sys.stderr)
                if cl['default'] is None:
                    print("Default value is automatically determined", file=sys.stderr)
                else:
                    print("Default value: {0}".format(cl['default']), file=sys.stderr)
        print(file=sys.stderr)

        print("ARGUMENTS", file=sys.stderr)
        for cl in [r for r in TestClient.command_line if r['name'] is None]:
            # Print argument usage and help
            print("    {0}".format(TestClient.get_arg_str(cl)).ljust(30), end="", file=sys.stderr)
            print(cl['description'], file=sys.stderr)
            if 'possible_values' in cl and cl['possible_values']:
                print("".ljust(30), end="", file=sys.stderr)
                print("Possible values: {0}".format(", ".join(cl['possible_values'])), file=sys.stderr)
        print(file=sys.stderr)

        if exit:
            sys.exit(exit_code)




    def read_arguments(self):

        arg_regex = re.compile('^(-[^=]+)(=(.*))?')

        for cl in TestClient.command_line:
            if not 'min_occurs' in cl:
                if cl['label'] is None or 'default' in cl:   # flags (without values) or settings with default values
                    cl['min_occurs'] = 0
                else:
                    cl['min_occurs'] = 1
            if not 'max_occurs' in cl:
                if cl['label'] is None:   # flags (without values)
                    cl['max_occurs'] = 0   # no limit
                else:
                    cl['max_occurs'] = 1
            cl['value_count'] = 0

        for arg in sys.argv[1:]:
            value = None
            match = arg_regex.match(arg)
            if match is None:  # it's an argument (i.e. only a value)
                cl = next((r for r in TestClient.command_line if r['name'] is None and r['value_count'] < r['max_occurs']), None)
                value = arg

            else:  # it's an option (with an optional value)
                cl = next((r for r in TestClient.command_line if r['name'] == match[1]), None)
                value = match[3]

            if cl is None:
                TestClient.print_usage("Invalid argument: {0}".format(arg))

            cl['value_count'] += 1

            # Check if value is correctly formatted
            if value == "":
                value = None

            if cl['min_occurs'] != 0 and value is None:
                TestClient.print_usage("Empty value provided for: {0}".format(cl['name']))

            if 'type' in cl:
                if cl['type'] == 'int':
                    try:
                        n = int(value)
                        value = n
                    except:
                        TestClient.print_usage("Not an integer value: {0}".format(arg))
                elif cl['type'] == 'float':
                    try:
                        n = float(value)
                        value = n
                    except:
                        TestClient.print_usage("Not a floating-point value: {0}".format(arg))

            if 'possible_values' in cl:
                if not value in cl['possible_values']:
                    TestClient.print_usage("Provided value is not among possible values: {0}".format(arg))

            # Check if value was provided too many times
            if cl['max_occurs'] != 0 and cl['value_count'] > cl['max_occurs']:
                TestClient.print_usage("Too many values provided for: {0}".format(cl['name']))

            self.set_property(cl['name'], cl['label'], value)


        # Check for missing options/arguments
        missing = []
        for cl in TestClient.command_line:
            if cl['value_count'] == 0 and 'default' in cl:
                self.set_property(cl['name'], cl['label'], cl['default'])
            elif cl['min_occurs'] != 0 and cl['value_count'] < cl['min_occurs']:
                missing.append(TestClient.get_arg_str(cl))

        if missing != []:
            TestClient.print_usage("Missing value(s) for: {0}".format(", ".join(missing)))



    def set_property(self, name, label, value):
        if name == '-h':
            TestClient.print_usage(exit_code=0)
        if name == '-v':
            Logger.verbose = True
        if name == '-ml':
            Logger.mixed_logs = True
        elif name == '-P':
            Logger.show_passwords = True
        elif name == '-K':
            TestClient.keep_vm = True
        elif name == '-X1':
            TestClient.exit_at = 1
        elif name == '-conf':
            self.config_file = value
        elif name == '-vm':
            if value < 1: TestClient.print_usage("Value for {0} must be at least 1".format(name))
            self.vm_count = value
        elif name == '-lf':
            if value < 1: TestClient.print_usage("Value for {0} must be at least 1".format(name))
            self.load_factor = value
        elif name == '-sp':
            self.service_provider = value
        elif name == '-ts':
            self.target_site = value
        elif name == '-te':
            self.target_endpoint = value
        elif name == '-tc':
            self.target_credentials = value
        elif name == '-psw':
            self.processing_scenario_cwl_file = value
        elif name == '-psi':
            self.processing_scenario_input_file = value
        elif name == '-i':
            self.docker_image_id = value
        elif name == '-a':
            self.docker_config = value
        elif name == '-n':
            self.test_site_name = value
        elif label == 'test-scenario':
            self.test_scenario_id = value




    def exit(exit_code, message):
        print("ERROR: {0}".format(message), file=sys.stderr)
        sys.exit(exit_code)



    def read_config_file(self):
        """Reads the config file sections relevant to the test scenario execution,
        based on the command-line arguments.
        """
        
        if not path.exists(self.config_file) or not path.isfile(self.config_file):
            exit_client(ERR_CONFIG, "Configuration file {0} does not exist".format(self.config_file))

        with open(self.config_file, 'r') as stream:
            try:
                full_config = yaml.safe_load(stream)
                stream.close()
            except yaml.YAMLError as e:
                exit_client(ERR_CONFIG, str(e))

            if 'global' not in full_config:
                exit_client(ERR_CONFIG, "Global configuration section not found")

            global_config = full_config['global']

            if 'docker_config' in global_config:
                if not self.docker_config:
                    self.docker_config = global_config['docker_config']
            else:
                exit_client(ERR_CONFIG, "No global configuration for docker authentication file found, and none specified in command (-a)")

            if 'ca_certificate' in global_config:
                if isinstance(global_config['ca_certificate'], list):
                    self.ca_certificates.extend(global_config['ca_certificate'])
                else:
                    self.ca_certificates.append(global_config['ca_certificate'])

            if 'connect_retries' in global_config:
                self.connect_retries = global_config['connect_retries']
            if 'connect_interval' in global_config:
                self.connect_interval = global_config['connect_interval']
            if 'max_retention_hours' in global_config:
                self.max_retention_hours = global_config['max_retention_hours']

            # Set service provider parameters
            if 'service_providers' in full_config:
                self.service_provider_configs = full_config['service_providers']
            else:
                exit_client(ERR_CONFIG, "Service provider configuration section not found")

            if self.service_provider not in self.service_provider_configs:
                exit_client(ERR_CONFIG, "No configuration found for service provider '{0}'".format(self.service_provider))

            self.service_provider_config = self.service_provider_configs[self.service_provider]

            if 'compute' not in self.service_provider_config:
                exit_client(ERR_CONFIG, "Service provider '{0}' does not contain a 'compute' configuration".format(self.service_provider))

            self.compute_config = self.service_provider_config['compute']

            if not isinstance(self.compute_config, dict):
                exit_client(ERR_CONFIG, "'compute' configuration for service provider '{0}' is empty or invalid".format(self.service_provider))

            # Read compute parameters
            for p in TestClient.compute_parameters:
                name = p['name']
                value = None
                if name in self.compute_config:
                    if self.compute_config[name] is not None:
                        value = self.compute_config[name]
                if value is None:
                    if ('default' in p):
                        if p['default'] is not None:
                            Logger.log(LogLevel.DEBUG, "No value found for configuration key '{0}' ({1}), using default: {2}".format(name, p['description'], p['default']))
                            value = p['default']

                if 'type' in p:
                    ptype = p['type']
                else:
                    ptype = None

                if 'list' in p and p['list']:
                    if not isinstance(value, list):
                        value = [ value ]
                    temp_value = []
                    for item in value:
                        if ptype == 'int':
                            try:
                                n = int(item)
                                temp_value.append(n)
                            except:
                                exit_client(ERR_CONFIG, "List value for parameter '{0}' not an integer value: {1}".format(name, item))
                        elif ptype == 'float':
                            try:
                                n = float(item)
                                temp_value.append(n)
                            except:
                                exit_client(ERR_CONFIG, "List value for parameter '{0}' not a floating-point value: {1}".format(name, item))
                        elif ptype == 'bool':
                            if isinstance(value, bool):
                                temp_value.append(item)
                            else:
                                item = item.lower()
                                if item == "true" or item == "yes":
                                    item = True
                                elif item == "false" or item == "no":
                                    item = False
                                else:
                                    exit_client(ERR_CONFIG, "List value for parameter '{0}' not a boolean value: {1}".format(name, item))
                        elif item is not None:
                            temp_value.append(str(item))

                    value = temp_value

                elif ptype == 'int':
                    try:
                        n = int(value)
                        value = n
                    except:
                        exit_client(ERR_CONFIG, "Value for '{0}' not an integer value: {1}".format(name, value))
                elif ptype == 'float':
                    try:
                        n = float(value)
                        value = n
                    except:
                        exit_client(ERR_CONFIG, "Value for '{0}' not a floating-point value: {1}".format(name, value))
                elif ptype == 'bool':
                    if not isinstance(value, bool):
                        value = value.lower()
                        if value == "true" or value == "yes":
                            value = True
                        elif value == "false" or value == "no":
                            value = False
                        else:
                            exit_client(ERR_CONFIG, "Value for '{0}' not a boolean value: {1}".format(name, value))
                elif value is not None:
                    value = str(value)

                self.compute_config[name] = value

        error = False
        for name in [ 'flavor_name', 'private_key_file', 'remote_user' ]:
            if name not in self.compute_config or self.compute_config[name] is None:
                error = True
                Logger.log(LogLevel.ERROR, "No value found for configuration key '{0}' ({1})".format(name, next((p['description'] for p in compute_parameters if 'name' in p and p['name'] == name and 'description' in p), 'no description available')))

        if error:
            exit_client(ERR_CONFIG, "Values missing for one or more configuration keys")

        self.flavor_count = len(self.compute_config['flavor_name'])
        if len(self.compute_config['cost_monthly']) != self.flavor_count:
            exit_client(ERR_CONFIG, "{0} value(s) required for monthly cost (as per flavour):".format(self.flavor_count))
        if len(self.compute_config['cost_hourly']) != self.flavor_count:
            exit_client(ERR_CONFIG, "{0} value(s) required for hourly cost (as per flavour):".format(self.flavor_count))

        if TestClient.keep_vm:
            self.compute_config['vm_name'] = "K-{0}".format(self.compute_config['vm_name'])




    def check_scenario(self):
        """Checks the information provided by the command-line arguments and the configuration file
        and verifies that the test scenario can be executed.
        """

        if not self.test_scenario_id in TestClient.test_scenarios:
            exit_client(ERR_CONFIG, "Test scenario '{0}' not configured".format(self.test_scenario_id))

        self.test_scenario = TestClient.test_scenarios[self.test_scenario_id]

        if self.test_scenario_id[0:4] == 'TS15':
            self.processing_scenario_id = self.test_scenario_id[5:]
            self.test_scenario_id = 'TS15'


        if self.docker_image_id is None:
            if 'docker_image_id' in self.test_scenario:
                self.docker_image_id = self.test_scenario['docker_image_id']
            else:
                exit_client(ERR_CONFIG, "No docker image ID configured for test scenario '{0}'".format(self.test_scenario_id))

        if self.docker_run_command is None:
            if 'docker_run_command' in self.test_scenario:
                self.docker_run_command = self.test_scenario['docker_run_command']
            else:
                exit_client(ERR_CONFIG, "No docker run command configured for test scenario '{0}'".format(self.test_scenario_id))

        if self.docker_run_command == 'CDAB_CLIENT_DEFAULT':
            if 'cdab_client_test_scenario_id' in self.test_scenario:
                self.cdab_client_test_scenario_id = self.test_scenario['cdab_client_test_scenario_id']
            else:
                exit_client(ERR_CONFIG, "No locally run test scenario configured for test scenario '{0}'".format(self.test_scenario_id))

            # Set target site parameters

            if self.target_endpoint is None or self.target_credentials is None:

                if self.target_site not in self.service_provider_configs:
                    exit_client(ERR_CONFIG, "No configuration found for target '{0}'".format(self.target_site))

                self.target_site_config = self.service_provider_configs[self.target_site]

                if 'data' not in self.target_site_config:
                    exit_client(ERR_CONFIG, "Service provider '{0}' does not contain a 'data' configuration".format(self.target_site))

                self.get_target_site_access()

            self.remote_cdab_json_file = "{0}Results.json".format(self.cdab_client_test_scenario_id)

        elif self.docker_run_command == 'PROCESSING':

            if not self.target_site:
                self.target_site = self.service_provider
                
            if self.target_site not in self.service_provider_configs:
                exit_client(ERR_CONFIG, "No configuration found for target '{0}'".format(self.target_site))

            self.target_site_config = self.service_provider_configs[self.target_site]

            if 'data' not in self.target_site_config:
                exit_client(ERR_CONFIG, "Service provider '{0}' does not contain a 'data' configuration".format(self.target_site))

            self.get_target_site_access()

            if not self.target_site_class:
                exit_client(ERR_CONFIG, "Service provider '{0}' configuration does not contain target site class ('class')".format(self.target_site))

            if not self.target_site_uri_prefix:
                exit_client(ERR_CONFIG, "Service provider '{0}' class is invalid (must be among {1})".format(self.target_site, ", ".join([s for s in TestClient.target_site_uri_prefixes])))

            credential_regex = re.compile('^([^:]+):(.*)')
            match = credential_regex.match(self.target_credentials)
            if match is None:  # it's an argument (i.e. only a value)
                self.target_site_username = ''
                self.target_site_password = ''
            else:  # it's an option (with an optional value)
                self.target_site_username = match[1]
                self.target_site_password = match[2]

            if self.test_scenario_id == "TS15":

                if not self.processing_scenario_cwl_file:
                    self.processing_scenario_cwl_file = "{0}/ts-scripts/workflow.{1}.cwl".format(os.path.dirname(sys.argv[0]), self.processing_scenario_id)

                if not path.exists(self.processing_scenario_cwl_file) or not path.isfile(self.processing_scenario_cwl_file):
                    exit_client(ERR_CONFIG, "Processing scenario CWL workflow file {0} does not exist".format(self.processing_scenario_cwl_file))

                if self.processing_scenario_input_file:
                    if not path.exists(self.processing_scenario_input_file) or not path.isfile(self.processing_scenario_input_file):
                        exit_client(ERR_CONFIG, "Processing scenario input YAML file {0} does not exist".format(self.processing_scenario_input_file))

                        
            if 'cdab_client_test_scenario_id' in self.test_scenario:
                self.cdab_client_test_scenario_id = self.test_scenario['cdab_client_test_scenario_id']


            self.remote_cdab_json_file = "{0}Results.json".format(self.test_scenario_id)

        if 'test_scenario_description' in self.test_scenario:
            self.test_scenario_description = self.test_scenario['test_scenario_description']
        else:
            self.test_scenario_description = "Test scenario {0}".format(self.cdab_client_test_scenario_id)


        if 'test_case_name' in self.test_scenario:
            self.test_case_name = self.test_scenario['test_case_name']
        else:
            exit_client(ERR_CONFIG, "No test case name configured for test scenario '{0}'".format(self.test_scenario_id))

        if 'test_target_url' in self.test_scenario:
            self.test_target_url = self.test_scenario['test_target_url']



    def get_target_site_access(self):
        """Gets endpoint and credentials of target site (data section).
        """
        data_config = self.target_site_config['data']

        if not isinstance(data_config, dict):
            exit_client(ERR_CONFIG, "'data' configuration for service provider '{0}' is empty or invalid".format(self.target_site))

        if 'url' in data_config:
            self.target_endpoint = data_config['url']
        else:
            exit_client(ERR_CONFIG, "Service provider '{0}' configuration does not contain target endpoint ('url')".format(self.target_site))

        if 'credentials' in data_config:
            self.target_credentials = data_config['credentials']
        else:
            exit_client(ERR_CONFIG, "Service provider '{0}' configuration does not contain target credentials".format(self.target_site))

        if 'class' in data_config:
            self.target_site_class = data_config['class']
            if self.target_site_class in TestClient.target_site_uri_prefixes:
                self.target_site_uri_prefix = TestClient.target_site_uri_prefixes[self.target_site_class]
            if self.target_site_class in TestClient.target_site_s3_uri_prefixes:
                self.target_site_s3_uri_prefix = TestClient.target_site_s3_uri_prefixes[self.target_site_class]

        if 's3_key_id' in data_config and 's3_secret_key' in data_config:
            self.target_site_s3_key_id = data_config['s3_key_id']
            self.target_site_s3_secret_key = data_config['s3_secret_key']

            




    def run_test(self):
        """Oversees the entire execution of the test scenario, which is split in
        individual test run threads (their number is the product of the number of
        requested virtual machines and the number of different flavours or
        machine types to be used).

        This method is the central method of the test scenario execution and it does the following:
        * Create an appropriate connector to the cloud provider,
        * Delete resources that might have been left over from previous test
          executions,
        * Create the resources (VMs, volumes, etc.) required for the test execution,
        * Start the threads for all test executions and waits for their termination,
        * Delete the resources previously created,
        * Produce the test scenario metrics from the downloaded test results.
        """

        Logger.log(LogLevel.INFO, "cdab-remote-client version {0}".format(TestClient.VERSION))

        if TestClient.exit_at == 1:
            Logger.log(LogLevel.INFO, "Main parameters for test execution:")
            print("- Configuration file:             {0}".format(self.config_file), file=sys.stderr)
            print("- CA certificates:                {0}".format(self.ca_certificates), file=sys.stderr)
            print("- Connection retries:             {0}".format(self.connect_retries), file=sys.stderr)
            print("- Connect retry interval (sec):   {0}".format(self.connect_interval), file=sys.stderr)
            print("- Number of virtual machines:     {0}".format(self.vm_count), file=sys.stderr)
            print("- Load factor:                    {0}".format(self.load_factor), file=sys.stderr)
            print("- Maximum parallelism:            {0}".format(self.max_parallelism), file=sys.stderr)
            print("- Service provider name:          {0}".format(self.service_provider), file=sys.stderr)
            print("- Target site:                    {0}".format(self.target_site), file=sys.stderr)
            print("- Target endpoint:                {0}".format(self.target_endpoint), file=sys.stderr)
            print("- Target credentials:             {0}".format(self.target_credentials), file=sys.stderr)
            print("- Docker configuration file:      {0}".format(self.docker_config), file=sys.stderr)
            print("- Docker image identifier:        {0}".format(self.docker_image_id), file=sys.stderr)
            print("- Test site name:                 {0}".format(self.test_site_name), file=sys.stderr)
            print("- Test scenario (TS):             {0}".format(self.test_scenario_id), file=sys.stderr)
            print("- Remote TS (if applicable):      {0}".format(self.cdab_client_test_scenario_id), file=sys.stderr)
            print("- Test scenario description:      {0}".format(self.test_scenario_description), file=sys.stderr)
            print("- Test case name:                 {0}".format(self.test_case_name), file=sys.stderr)
            print(file=sys.stderr)
            Logger.log(LogLevel.INFO, "Compute settings:")

            for c in self.compute_config:
                print("- {0}{1}".format("{0}:".format(c).ljust(32), self.compute_config[c]))

            print(file=sys.stderr)

            Logger.log(LogLevel.INFO, "Exiting")
            sys.exit(0)

        # Create connector based on configuration
        if 'connector' in self.compute_config:
            connector_str = self.compute_config['connector'].lower()
            if connector_str == 'openstack':
                self.connector = openstack.OpenStackConnector(self)
            elif connector_str == 'google':
                self.connector = google.GoogleConnector(self)
            elif connector_str == 'amazon':
                self.connector = amazon.AmazonConnector(self)
            else:
                exit_client(ERR_CONFIG, "Unknown connector: {0}".format(self.compute_config['connector']))

        if self.connector is None:
            self.connector = openstack.OpenStackConnector(self)   # default, for backward compatibility

        self.total_vm_count = self.flavor_count * self.vm_count

        self.connector.initialize(TestClient.compute_parameters)

        Logger.log(LogLevel.INFO, "Checking for old resources to delete ...")
        self.connector.delete_old_resources(self.max_retention_hours)
        Logger.log(LogLevel.INFO, "Done")

        self.connector.prepare()

        # Get random sequence for VM names to avoid conflicts
        random_sequence = str(uuid.uuid4())[0:8]

        Logger.log(LogLevel.INFO, "Start of execution")

        self.start_time = datetime.datetime.utcnow()

        runs = []
        threads = []

        index = 0
        for fi, flavor in enumerate(self.compute_config['flavor_name']):
            for i in range(self.vm_count):
                short_name = "#{0}".format(index + 1)
                if self.total_vm_count == 1:
                    name = "Test run"
                    suffix = "-{0}".format(random_sequence)
                else:
                    name = "Parallel test run #{0}".format(index + 1)
                    if self.flavor_count > 1:
                        name += " (flavour: '{0}')".format(flavor)
                    suffix = "-{0}-{1}".format(random_sequence, index + 1)
                if self.total_vm_count == 1 or Logger.mixed_logs:
                    stderr = sys.stderr
                else:
                    stderr = io.StringIO()


                
                run = TestRun(index, suffix, short_name, name, flavor, self.compute_config['cost_monthly'][fi], self.compute_config['cost_hourly'][fi], self.compute_config['currency'], stderr)
                runs.append(run)
                index += 1

                thread = threading.Thread(target=self.run_single_test, args=(run,))
                run.thread = thread
                threads.append(thread)
                thread.start()
            fi += 1

        if self.total_vm_count != 1 and not Logger.mixed_logs:
            Logger.log(LogLevel.INFO, "Logs will be available when threads have finished")
            print(file=sys.stderr) # empty line to separate logs


        for run in runs:
            # Logger.log(LogLevel.DEBUG, "Waiting for run '{0}' to finish".format(run.name))
            run.thread.join()
            if self.total_vm_count == 1 or Logger.mixed_logs:
                Logger.log(LogLevel.INFO, "{0} finished{1}".format(run.name, " with error" if run.test_end_time is None else ''))
            else:
                Logger.log(LogLevel.INFO, "{0} finished{1} (logs below)".format(run.name, " with error" if run.test_end_time is None else ''))
                print(run.stderr.getvalue(), file=sys.stderr)
                run.stderr.close()

        for run in runs:
            print("--------------------------------------------------------------------", file=sys.stderr)
            print("Timing summary for {0}".format(run.name), file=sys.stderr)
            print("* VM creation request:                   {0}".format(TestClient.get_time_str(run.create_start_time)), file=sys.stderr)
            print("* VM ready to use:                       {0}".format(TestClient.get_time_str(run.ssh_ready_time)), file=sys.stderr)
            print("* Docker and image installation started: {0}".format(TestClient.get_time_str(run.install_start_time)), file=sys.stderr)
            print("* Test started:                          {0}".format(TestClient.get_time_str(run.test_start_time)), file=sys.stderr)
            print("* Test finished:                         {0}".format(TestClient.get_time_str(run.test_end_time)), file=sys.stderr)
            print("* Test results downloaded:               {0}".format(TestClient.get_time_str(run.files_downloaded_time)), file=sys.stderr)
            print("* VM deleted:                            {0}".format(TestClient.get_time_str(run.delete_end_time)), file=sys.stderr)

        print("--------------------------------------------------------------------", file=sys.stderr)

        self.end_time = datetime.datetime.utcnow()


        # Analyse results and create new file
        self.produce_metrics([r for r in runs if r.ssh_ready_time], len(runs))

        Logger.log(LogLevel.INFO, "End of execution")

        if self.incomplete_deletion:
            print("********************************************************************", file=run.stderr)
            Logger.log(LogLevel.WARN, "Some virtual cloud resources might not have been deleted", run=run)
            Logger.log(LogLevel.WARN, "Check previous error messages", run=run)
            print("********************************************************************", file=run.stderr)



    def run_single_test(self, run):
        """Executes a single test run. This method is the main method in the test run execution thread.
        
        Parameters
        ----------
        run : TestRun
            The test run object encapsulating all information for an individual test run.
        """
        try:
            if self.connector.create_vm(run):
                self.run_remote_commands(run)
        except Exception as e:
            Logger.log(LogLevel.ERROR, str(e), run=run)
        finally:
            if TestClient.keep_vm:
                Logger.log(LogLevel.WARN, "Virtual machine is not deleted, as requested", run=run)
            else:
                self.connector.delete_vm(run)



    def run_remote_commands(self, run):
        """Executes the remote commands for a single run of the test scenario.
        This method is called when the virtual machine is ready to accept
        remote shell commands (ssh or scp).

        The contains scenario-specific installtion of software, transfer of files,
        e.g. for confuguration, and eventually the execution of the actual test
        scenario and the download of test results.

        Parameters
        ----------
        run : TestRun
            The test run object encapsulating all information for an individual test run.
        """
        Logger.log(LogLevel.INFO, "Installing and starting docker ...", run=run)

        run.install_start_time = datetime.datetime.utcnow()

        # Software installation
        execute_remote_command(self.compute_config, run, "sudo yum install -y yum-utils device-mapper-persistent-data lvm2")
        execute_remote_command(self.compute_config, run, "sudo yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo")
        execute_remote_command(self.compute_config, run, "sudo yum install docker-ce docker-ce-cli containerd.io -y")

        # Preparation for volume use (in case main disk is too small)
        if self.compute_config['use_volume']:
            execute_remote_command(self.compute_config, run, "sudo systemctl start docker") # to create /var/lib/docker directory
            execute_remote_command(self.compute_config, run, "sudo systemctl stop docker")
            execute_remote_command(self.compute_config, run, "sudo mv /var/lib/docker /mnt/cdab-volume/docker")
            # fails since 2020-07 (or earlier):
            # execute_remote_command(self.compute_config, run, "sudo mv /var/lib/docker-engine /mnt/cdab-volume/docker-engine")
            execute_remote_command(self.compute_config, run, "sudo ln -s /mnt/cdab-volume/docker /var/lib/docker")
            execute_remote_command(self.compute_config, run, "sudo ln -s /mnt/cdab-volume/docker-engine /var/lib/docker-engine")

        # Copy CA certificates if needed
        if self.ca_certificates:
            Logger.log(LogLevel.INFO, "Copying {0} CA certificate(s) ...".format(len(self.ca_certificates)), run=run)
            for cert in self.ca_certificates:
                cert_basename = os.path.basename(cert)
                copy_file(self.compute_config, run, cert, cert_basename)
                execute_remote_command(self.compute_config, run, "sudo mv {0} /etc/pki/ca-trust/source/anchors/{0}".format(cert_basename))
                execute_remote_command(self.compute_config, run, "sudo chown root:root /etc/pki/ca-trust/source/anchors/{0}".format(cert_basename))
            execute_remote_command(self.compute_config, run, "sudo update-ca-trust")
            Logger.log(LogLevel.INFO, "CA certificate(s) copied", run=run)

        # Start Docker service
        execute_remote_command(self.compute_config, run, "sudo systemctl start docker")
        execute_remote_command(self.compute_config, run, "sudo usermod -a -G docker $USER")
        Logger.log(LogLevel.INFO, "Docker service started", run=run)

        # Copy Docker authentication file
        execute_remote_command(self.compute_config, run, "mkdir .docker")
        copy_file(self.compute_config, run, self.docker_config, ".docker/config.json")

        # Install Docker image
        if self.docker_image_id:
            Logger.log(LogLevel.INFO, "Installing docker image for test ...", run=run)
            execute_remote_command(self.compute_config, run, "docker pull {0}".format(self.docker_image_id))
            Logger.log(LogLevel.INFO, "Docker image installed", run=run)

        run.test_start_time = datetime.datetime.utcnow()

        if self.compute_config['use_volume']:
            working_dir = "/mnt/cdab-volume/test"
            execute_remote_command(self.compute_config, run, "sudo mkdir -p {0}".format(working_dir))
            execute_remote_command(self.compute_config, run, "sudo chown {0}:{0} {1}".format(self.compute_config['remote_user'], working_dir))
        else:
            working_dir = "."   # remote user's home directory

        copy_file(self.compute_config, run, self.config_file, "{0}/config.yaml".format(working_dir))
        try:
            self.connector.copy_additional_files(run)
        except Exception as e:
            Logger.log(LogLevel.WARN, str(e), run=run)

        if self.docker_run_command == 'CDAB_CLIENT_DEFAULT':

            script_name = "{0}-remote.sh".format(self.test_scenario_id)
            copy_file(self.compute_config, run, "{0}/ts-scripts/{1}".format(os.path.dirname(sys.argv[0]), script_name), script_name)

            Logger.log(LogLevel.INFO, "Running test scenario {0} (using cdab-client) ...".format(self.cdab_client_test_scenario_id), run=run)

            execute_remote_command(
                self.compute_config,
                run,
                "nohup sh {0} {1} {2} {3} {4} {5} {6} {7} {8} > /dev/null 2>&1 &".format(
                    script_name,
                    working_dir,
                    self.docker_image_id,
                    self.target_site,
                    self.target_endpoint,
                    self.target_credentials,
                    self.test_site_name,
                    self.load_factor,
                    self.compute_config['download_origin'],
                ),
                display_command="nohup sh {0} {1} {2} {3} {4} {5} {6} {7} {8} > /dev/null 2>&1 &".format(
                    script_name,
                    working_dir,
                    self.docker_image_id,
                    self.target_site,
                    self.target_endpoint,
                    re.sub(':.*', ':xxxxxxxx', self.target_credentials),
                    self.test_site_name,
                    self.load_factor,
                    self.compute_config['download_origin'],
                )
            )

        elif self.docker_run_command == 'PROCESSING':
            if self.test_scenario_id == "TS15":
                script_name = "{0}.{1}-remote.sh".format(self.test_scenario_id, self.processing_scenario_id)
                copy_file(self.compute_config, run, self.processing_scenario_cwl_file, "{0}/workflow.cwl".format(working_dir))
                if self.processing_scenario_input_file:
                    copy_file(self.compute_config, run, self.processing_scenario_input_file, "{0}/input".format(working_dir))
            else:
                script_name = "{0}-remote.sh".format(self.test_scenario_id)
            
            if 'tools' in self.test_scenario:
                if 'conda' in self.test_scenario['tools']:
                    copy_file(self.compute_config, run, "{0}/ts-scripts/conda-install.sh".format(os.path.dirname(sys.argv[0])), "conda-install.sh")
                    execute_remote_command(self.compute_config, run, "sudo sh conda-install.sh")

                if 'opensearch-client' in self.test_scenario['tools']:
                    execute_remote_command(self.compute_config, run, "sudo yum install -y unzip yum-utils")
                    execute_remote_command(self.compute_config, run, "sudo yum-config-manager --add-repo http://download.mono-project.com/repo/centos/")
                    execute_remote_command(self.compute_config, run, "sudo yum install -y mono-devel --nogpgcheck > /dev/null 2> /dev/null")
                    copy_file(self.compute_config, run, "{0}/ts-scripts/opensearch-client.zip".format(os.path.dirname(sys.argv[0])), "opensearch-client.zip")
                    execute_remote_command(self.compute_config, run, "sudo unzip -d /usr/lib/ opensearch-client.zip")
                    execute_remote_command(self.compute_config, run, "sudo mv /usr/lib/opensearch-client/bin/opensearch-client /usr/bin/")

                if 'Stars' in self.test_scenario['tools']:
                    execute_remote_command(self.compute_config, run, "docker pull terradue/stars-t2:latest")
                    execute_remote_command(self.compute_config, run, "mkdir -p config/Stars")
                    execute_remote_command(self.compute_config, run, "mkdir -p config/etc/Stars")

                    # Add specific supplier
                    self.connector.add_supplier(TestClient.stars_plugins['Plugins']['Terradue']['Suppliers'])

                    with open("stars-terradue.json", 'w') as stars_file:
                        stars_file.write(json.dumps(TestClient.stars_plugins, indent=4))
                        stars_file.close()
                    copy_file(self.compute_config, run, "stars-terradue.json", "config/etc/Stars/terradue.json")

                    credential_config = {
                        'Credentials': {
                            'supplier': {
                                'Type': "Basic",
                                'UriPrefix': self.target_site_uri_prefix,
                                'Username': self.target_site_username,
                                'Password': self.target_site_password,
                            }
                        }
                    }

                    if self.target_site_s3_key_id and self.target_site_s3_secret_key and self.target_site_s3_uri_prefix:
                        credential_config['Credentials']['s3_supplier'] = {
                            'AuthType': "S3",
                            'UriPrefix': self.target_site_s3_uri_prefix,
                            "Username": self.target_site_s3_key_id,
                            "Password": self.target_site_s3_secret_key
                        }

                    with open("stars-usersettings.json", 'w') as stars_file:
                        stars_file.write(json.dumps(credential_config, indent=4))
                        stars_file.close()
                    copy_file(self.compute_config, run, "stars-usersettings.json", "config/Stars/usersettings.json")

            if 'files' in self.test_scenario:
                for f in self.test_scenario['files']:
                    copy_file(self.compute_config, run, "{0}/ts-scripts/{1}".format(os.path.dirname(sys.argv[0]), f), "{0}/{1}".format(working_dir, f))


            Logger.log(LogLevel.INFO, "Running processing test scenario {0} ...".format(self.test_scenario_id), run=run)

            copy_file(self.compute_config, run, "{0}/ts-scripts/{1}".format(os.path.dirname(sys.argv[0]), script_name), script_name)
    
            execute_remote_command(
                self.compute_config,
                run,
                "nohup sh {0} {1} \"{2}\" {3} {4} {5} > /dev/null 2>&1 &".format(
                    script_name,
                    working_dir,
                    self.docker_image_id,
                    self.test_site_name,
                    self.target_site_class,
                    self.target_credentials
                ),
                display_command="nohup sh {0} {1} \"{2}\" {3} {4} {5} > /dev/null 2>&1 &".format(
                    script_name,
                    working_dir,
                    self.docker_image_id,
                    self.test_site_name,
                    self.target_site_class,
                    re.sub(':.*', ':xxxxxxxx', self.target_credentials),
                )
            )

        if 'timeout' in self.test_scenario:
            timeout = self.test_scenario['timeout']
        else:
            timeout = 2 * 60 * 60
        max_end_time = datetime.datetime.utcnow() + datetime.timedelta(seconds=timeout)
        Logger.log(LogLevel.INFO, "{0} - {1} - {2}".format(datetime.datetime.utcnow(), timeout, max_end_time), run=run)
        Logger.log(LogLevel.INFO, "Maximum allowed end time of processing: {0}".format(max_end_time.strftime('%Y-%m-%dT%H:%M:%S.%fZ')), run=run)
        running = True
        while running and datetime.datetime.utcnow() < max_end_time:
            output = execute_remote_command(self.compute_config, run, "ps auxw | grep {0} | grep -v grep | cat".format(script_name))
            #Logger.log(LogLevel.DEBUG, "OUTPUT: {0}".format(output), run=run)
            if output:
                Logger.log(LogLevel.DEBUG, "Process still running, wait for 30 seconds", run=run)
                time.sleep(30)
            else:
                running = False

        run.test_end_time = datetime.datetime.utcnow()
        Logger.log(LogLevel.INFO, "Test completed", run=run)

        stdout_file = "cdab{0}.stdout".format(run.suffix)
        stderr_file = "cdab{0}.stderr".format(run.suffix)

        copy_file(self.compute_config, run, run.cdab_json_file, "{0}/{1}".format(working_dir, self.remote_cdab_json_file), False)
        copy_file(self.compute_config, run, run.junit_file, "{0}/junit.xml".format(working_dir), False)
        copy_file(self.compute_config, run, stdout_file, "{0}/cdab.stdout".format(working_dir), False)
        copy_file(self.compute_config, run, stderr_file, "{0}/cdab.stderr".format(working_dir), False)
        run.files_downloaded_time = datetime.datetime.utcnow()

        Logger.log(LogLevel.INFO, "Test result files received", run=run)
        Logger.log(LogLevel.INFO, "stdout and stderr from cdab-client execution on virtual machine below", run=run)

        Logger.log(LogLevel.INFO, "--------------------------------", run=run)
        Logger.log(LogLevel.INFO, "remote execution stdout (START)", run=run)
        with open(stdout_file, 'r') as cdab_stdout:
            print(cdab_stdout.read(), end="", file=run.stderr)
        Logger.log(LogLevel.INFO, "remote execution stdout (END)", run=run)
        Logger.log(LogLevel.INFO, "remote execution stderr (START)", run=run)
        with open(stderr_file, 'r') as cdab_stderr:
            print(cdab_stderr.read(), end="", file=run.stderr)
        Logger.log(LogLevel.INFO, "remote execution stderr (END)", run=run)
        Logger.log(LogLevel.INFO, "--------------------------------", run=run)





    def produce_metrics(self, runs, total_runs):
        """Receives the metrics from the test executions and aggregates them to the overall metrics.

        Parameters
        ----------
        runs : list of TestRun instances
            The test run objects encapsulating all information for an individual test run.
            The list contains only test runs for which a virtual machine was successfully created.
        total_runs : the number of total test runs (can be different from the lengths of runs)
        """

        test_target_url = self.target_endpoint

        # Read remote test results JSON files (copy testCaseResults nodes)
        all_test_case_nodes = []
        test_case_nodes = []
        for run in runs:
            if not run.test_end_time:
                continue   # if there is no end time for a run, there are no output files
            with open(run.cdab_json_file, 'r') as file:
                result_file = json.loads(file.read())
                if test_target_url is None and 'testTargetUrl' in result_file and result_file['testTargetUrl']:
                    test_target_url = result_file['testTargetUrl']
                
                for test_case_result_node in result_file['testCaseResults']:
                    # If main test case of test scenario (as configured) is already dealt with on VM,
                    # extract error rate and process duration.
                    if 'testName' in test_case_result_node and 'metrics' in test_case_result_node and test_case_result_node['testName'] == self.test_case_name:
                        for m in test_case_result_node['metrics']:
                            if 'name' not in m or 'value' not in m:
                                continue
                            if m['name'] == 'errorRate' and isinstance(m['value'], float):
                                run.error_rate = m['value']
                            if m['name'] == 'processDuration' and isinstance(m['value'], int):
                                run.process_duration = m['value']
                            if m['name'] == 'avgProcessDuration' and isinstance(m['value'], int):
                                run.avg_process_duration = m['value']
                            if m['name'] == 'processCount' and isinstance(m['value'], int):
                                run.process_count = m['value']
                        continue   # don't add main test case of test scenario

                    all_test_case_nodes.append(test_case_result_node)
            
                file.close()

        if test_target_url is None:
            test_target_url = self.test_target_url

        aggregate_functions = {
            'avgResponseTime': lambda l: TestClient.get_average(l),
            'errorRate': lambda l: TestClient.get_average(l, 2),
            'avgConcurrency': sum,
            'avgSize': lambda l: TestClient.get_average(l),
            'resultsErrorRate': lambda l: TestClient.get_average(l, 2),
            'peakResponseTime': max,
            'maxSize': max,
            'peakConcurrency': sum,
            'maxTotalResults': max,
            'totalReadResults': sum,
            'totalSize': sum,
            'throughput': sum,
            'dataCollectionDivision': lambda l: [i for l1 in l for i in l1],
            'processCount': sum,
            'avgProcessDuration': lambda l: TestClient.get_average(l),
        }

        # List of remote test cases (unique entries)
        test_cases = sorted(set([ t['testName'] for t in all_test_case_nodes ]))

        for test_case in test_cases:
            orig_nodes = [ t for t in all_test_case_nodes if t['testName'] == test_case ]
            test_name = test_case if test_case.startswith('TC') else "TC{0}".format(test_case)
            merged_node = { 'testName': test_name }
            merged_node['className'] = next(t['className'] for t in orig_nodes if 'className' in t)
            seq = [t['startedAt'] for t in orig_nodes if 'startedAt' in t and t['startedAt']]
            merged_node['startedAt'] = min(seq) if seq else None
            seq = [t['endedAt'] for t in orig_nodes if 'endedAt' in t and t['endedAt']]
            merged_node['endedAt'] = max(seq) if seq else None
            durations = [t['duration'] for t in orig_nodes if 'duration' in t]
            merged_node['duration'] = int(round(sum(durations) / len(durations)))

            merged_node['metrics'] = []
            metric_names = []
            for t in orig_nodes:
                for m in t['metrics']:
                    if m['name'] not in metric_names:
                        metric_names.append(m['name'])
            for metric_name in metric_names:
                metrics = [ m for t in orig_nodes for m in t['metrics'] if m['name'] == metric_name ]
                merged_metric = metrics[0].copy()
                if metric_name in aggregate_functions:
                    merged_metric['value'] = aggregate_functions[metric_name]([m['value'] for m in metrics])
                merged_node['metrics'].append(merged_metric)

            search_filters = [ s for t in orig_nodes if 'searchFiltersDefinition' in t and t['searchFiltersDefinition'] for s in t['searchFiltersDefinition'] ]
            if search_filters:
                merged_node['searchFiltersDefinition'] = search_filters

            other_node_names = []
            for t in orig_nodes:
                for k in t:
                    if k not in ['testName', 'className', 'startedAt', 'endedAt', 'duration', 'metrics', 'searchFiltersDefinition' ] and k not in other_node_names:
                        other_node_names.append(k)

            for other_node_name in other_node_names:
                nodes = [ t[other_node_name] for t in orig_nodes if other_node_name in t ]
                merged_node[other_node_name] = nodes

            test_case_nodes.append(merged_node)

        
        # Calculate metrics
        for run in runs:
            if run.delete_end_time:
                run.duration = round((run.delete_end_time - run.create_start_time).total_seconds() * 1000)
            else:
                run.duration = round((datetime.datetime.utcnow() - run.create_start_time).total_seconds() * 1000)
            if run.test_start_time and run.test_end_time:
                if not run.process_duration:
                    run.process_duration = round((run.test_end_time - run.test_start_time).total_seconds() * 1000)
            else:
                if not run.process_duration:
                    run.process_duration = -1
            run.provisioning_latency = round((run.ssh_ready_time - run.create_start_time).total_seconds() * 1000)

        if len(runs) == 0:
            error_rate = 100.0
            avg_concurrency = 0
            peak_concurrency = 0
            avg_provisioning_latency = -1
            total_duration = 0
        elif len(runs) == 1:
            run = runs[0]
            if run.error_rate == 0 and not run.test_end_time:
                run.error_rate = 100.0
                error_rate = 100.0
            else:
                error_rate = run.error_rate
            avg_concurrency = 1
            peak_concurrency = 1
            avg_provisioning_latency = run.provisioning_latency
            total_duration = run.duration
        else:
            parallel = 0
            next_start, next_end = None, None
            times = []
            for i, run in enumerate(runs):
                times.append({'time': run.create_start_time, 'type': 'S', 'run': i + 1})   # time, it's the start time of the creation, run number
                times.append({'time': run.ssh_ready_time, 'type': 'E', 'run': i + 1})      # time, it's the end time of the creation, run number

            # Events (i.e. start or end of the VM creation) must be sorted by time, if equal by type (end before start), if equal by sequence number of run
            times = sorted(times, key=lambda t: (t['time'], t['type'], t['run']))

            successful_runs = [r for r in runs if r.test_end_time and r.error_rate == 0]

            error_rate = round((1 - len(successful_runs) / total_runs) * 100, 1)

            parallel = 0
            overall_ms = 0     # total duration
            cumulated_ms = 0   # duration of all periods combined (greater than or equal overal_ms)
            last_time = None
            peak_concurrency = 1
            for t in times:
                if parallel != 0 and last_time:
                    ms = round((t['time'] - last_time).total_seconds() * 1000)
                    overall_ms += ms
                    cumulated_ms += parallel * ms

                if t['type'] == 'S':
                    parallel += 1
                    if parallel > peak_concurrency:
                        peak_concurrency = parallel
                else:
                    parallel -= 1

                last_time = t['time']

            avg_concurrency = round(cumulated_ms / overall_ms, 3)
            ms = 0
            for run in runs:
                ms += run.provisioning_latency
            avg_provisioning_latency = round(ms / len(runs))

            total_duration = 0
            for run in runs:
                if run.delete_end_time:
                    total_duration += run.duration

        print("Obtained metrics", file=sys.stderr)
        print("* Error rate (%):                        {0}".format(error_rate), file=sys.stderr)
        print("* Total duration (ms):                   {0}".format(total_duration), file=sys.stderr)
        print("* Average provisioning latency (ms):     {0}".format("n/a" if avg_provisioning_latency == -1 else avg_provisioning_latency), file=sys.stderr)
        print("* Average concurrency (#):               {0}".format(avg_concurrency), file=sys.stderr)
        print("* Peak concurrency (#):                  {0}".format(peak_concurrency), file=sys.stderr)
        for run in runs:
            print("* Run '{0}'".format(run.name), file=sys.stderr)
            print("  - Cost per hour ({1}):                 {0}".format(run.cost_hourly, run.currency), file=sys.stderr)
            print("  - Cost per month ({1}):                {0}".format(run.cost_monthly, run.currency), file=sys.stderr)
            print("  - Duration (ms):                       {0}".format(run.duration), file=sys.stderr)
            print("  - Process duration (ms):               {0}".format(run.process_duration), file=sys.stderr)
            print("  - Provisioning latency (ms):           {0}".format(run.provisioning_latency), file=sys.stderr)
        print("--------------------------------------------------------------------", file=sys.stderr)

        test_case_class = "cdabtesttools.TestCases.TestCase{0}".format(self.test_case_name.replace("TC", ""))

        result = {
            'jobName': os.environ['JOB_NAME'] if 'JOB_NAME' in os.environ else None,
            'buildNumber': os.environ['BUILD_NUMBER'] if 'BUILD_NUMBER' in os.environ else None,
            'testScenario': self.test_scenario_id,
            'testSite': self.service_provider,
            'testTargetUrl': test_target_url,
            'testTarget': self.target_site,
            'zoneOffset': "+00",
            'hostName': socket.gethostname(),
            'hostAddress': next((ni.ifaddresses(i)[ni.AF_INET][0]['addr'] for i in ni.interfaces() if ni.AF_INET in ni.ifaddresses(i) and ni.ifaddresses(i)[ni.AF_INET][0]['addr'] != '127.0.0.1'), None),
            'testCaseResults': test_case_nodes,
        }

        metrics = [
            {
                'name': "errorRate",
                'value': error_rate,
                'uom': "%"
            },
            {
                'name': "totalDuration",
                'value': total_duration,
                'uom': "ms"
            },
            {
                'name': "avgProvisioningLatency",
                'value': avg_provisioning_latency,
                'uom': "ms"
            },
            {
                'name': "avgConcurrency",
                'value': avg_concurrency,
                'uom': "#"
            },
            {
                'name': "peakConcurrency",
                'value': peak_concurrency,
                'uom': "#"
            },
            {
                'name': "dataSummaryRun",
                'value': [r.name for r in runs],
                'uom': "string"
            },
            {
                'name': "flavorName",
                'value': [r.flavor for r in runs],
                'uom': "string"
            },
            {
                'name': "costHour",
                'value': [r.cost_hourly for r in runs],
                'uom': self.compute_config['currency']
            },
            {
                'name': "costMonth",
                'value': [r.cost_monthly for r in runs],
                'uom': self.compute_config['currency']
            },
            {
                'name': "duration",
                'value': [r.duration for r in runs],
                'uom': "ms"
            },
            {
                'name': "processDuration",
                'value': [r.process_duration for r in runs],
                'uom': "ms"
            },
            {
                'name': "provisioningLatency",
                'value': [r.provisioning_latency for r in runs],
                'uom': "ms"
            },
        ]

        if [r.avg_process_duration for r in runs if r.avg_process_duration is not None]:
            metrics.append({
                'name': "avgProcessDuration",
                'value': [r.avg_process_duration for r in runs],
                'uom': "ms"
            })
        if [r.process_count for r in runs if r.process_count is not None]:
            metrics.append({
                'name': "processCount",
                'value': [r.process_count for r in runs],
                'uom': "#"
            })



        result['testCaseResults'].append({
            'testName': self.test_case_name,
            'className': test_case_class,
            'startedAt': TestClient.get_time_str(self.start_time),
            'endedAt': TestClient.get_time_str(self.end_time),
            'duration': round((self.end_time - self.start_time).total_seconds() * 1000),
            'metrics': metrics
        })

        output_file = "{0}Results.json".format(self.test_scenario_id)
        with open(output_file, 'w') as file:
            file.write(json.dumps(result, indent=2))
            file.close()
        Logger.log(LogLevel.INFO, "Output file written: {0}".format(output_file))

        # Read remote junit.xml files (copy testcase elements and count errors)
        testcase_elems = []
        if len(runs) == total_runs:
            error_count = 0
        else:
            error_count = 1

        for run in runs:
            if not run.test_end_time:
                continue
            tree = ET.parse(run.junit_file)
            testsuite_elem = tree.find('./testsuite')
            if testsuite_elem.attrib['errors']:
                error_count += int(testsuite_elem.attrib['errors'])
            testcase_elems.extend(tree.findall('./testsuite/testcase'))

        root_elem = ET.Element('testsuites')
        root_elem.attrib['xmlns:xsd'] = "http://www.w3.org/2001/XMLSchema"
        root_elem.attrib['xmlns:xsi'] = "http://www.w3.org/2001/XMLSchema-instance"
        testsuite_elem = ET.SubElement(root_elem, 'testsuite')
        testsuite_elem.attrib['name'] = self.test_scenario_description
        testsuite_elem.attrib['id'] = self.test_scenario
        testsuite_elem.attrib['errors'] = str(error_count)
        for testcase_elem in testcase_elems:
            testsuite_elem.append(testcase_elem)

        testsuite_elem.append(ET.Element('testcase', attrib={ 'name': self.test_case_name, 'classname': test_case_class, 'status': "OK" if error_rate == 0 else "ERROR" }))

        tree = ET.ElementTree(root_elem)
        tree.write("junit.xml.tmp", xml_declaration=True,encoding='utf-8', method="xml")
        with open("junit.xml", "w") as file:
            execute_local_command(None, ["xmllint", "--format", "junit.xml.tmp"], stdout = file)
            file.close()
        os.remove("junit.xml.tmp")

        Logger.log(LogLevel.INFO, "Output file written: {0}".format("junit.xml"))





    def get_arg_str(cl):
        if cl['name'] is None:
            return "<{0}>".format(cl['label'])
        else:
            arg = cl['name']
            if cl['label']:
                arg += "=<{0}>".format(cl['label'])
            return arg



    def get_time_str(time):
        if time is None:
            return "--"
        return time.strftime('%Y-%m-%dT%H:%M:%S.%fZ')



    def get_average(l, decimals=0):
        if len(l) == 0:
            return "N/A"
        for i in l:
            if not isinstance(i, int) and not isinstance(i, float):
                return "N/A"
        result = sum(l) / len(l)
        if (decimals == 0):
            return int(round(result))
        else:
            return round(result, decimals)




class TestRun:
    """Contains properties for individual test executions.
    Usually one instance corresponds to one thread, each of which handles a test scenario
    execution on a dedicated virtual machine.
    """

    def __init__(self, index, suffix, short_name, name, flavor, cost_monthly, cost_hourly, currency, stderr):
        self.suffix = suffix
        self.index = index
        self.short_name = short_name
        self.name = name
        self.flavor = flavor
        self.cost_monthly = cost_monthly
        self.cost_hourly = cost_hourly
        self.currency = currency
        self.stderr = stderr
        self.vm_id = None
        self.public_ip = None
        self.volume_id = None
        self.volume_attached = False
        self.volume_device = None
        self.tmp_volume_id = None
        self.tmp_volume_attached = False
        self.tmp_volume_device = None
        self.junit_file = "junit-remote-{0}.xml".format(suffix)
        self.cdab_json_file = "TestResult-remote{0}.json".format(suffix)
        self.create_start_time = None
        self.ssh_ready_time = None
        self.install_start_time = None
        self.test_start_time = None
        self.test_end_time = None
        self.files_downloaded_time = None
        self.delete_end_time = None
        self.duration = None
        self.error_rate = 0
        self.process_duration = None
        self.avg_process_duration = None
        self.process_count = None
        self.provisioning_latency = None




class LogLevel(Enum):
    ERROR = 1
    WARN  = 2
    INFO  = 3
    DEBUG = 4


client = TestClient()
client.run_test()
