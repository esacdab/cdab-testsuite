import sys
import yaml
import json
from loguru import logger
from test_scenarios import test_scenario
from connectors import cdse
import re

arg_regex = re.compile(r"^(?P<name>-+[^=]+)(=(?P<value>.*))?$")


class PyCdab:

    test_scenarios = {
        'TS01': test_scenario.TestScenario01,
        'TS02': test_scenario.TestScenario02,
    }

    def __init__(self, args):
        self.verbose = False
        self.config_file_name = None
        self.load_factor = 1
        self.service_provider_name = None
        self.test_scenario_name = None

        self.parse_arguments(args)
        self.check_settings()

        logger.remove()
        if self.verbose:
            logger.add(sys.stderr, level="DEBUG")
        else:
            logger.add(sys.stderr, level="INFO")


    def parse_arguments(self, args):
        for arg in args[1:]:
            match = arg_regex.match(arg)
            if match:
                name = match.group('name')
                value = match.group('value')
                if name == '-v' and not value:
                    self.verbose = True
                elif name == '--conf' and value:
                    self.config_file_name = value
                elif name == '--lf' and value:
                    self.load_factor = int(value)
                elif name == '--sp' and value:
                    self.service_provider_name = value
                else:
                    raise Exception("Invalid argument in command line: {0}".format(arg))
            else:
                self.test_scenario_name = arg


    def check_settings(self):
        if self.config_file_name is None:
            raise Exception("No value for --conf provided")
        if isinstance(self.load_factor, str):
            if re.match(r"\d+", self.load_factor):
                self.load_factor = int(self.load_factor)
            else:
                raise Exception("Value for --lf must be an integer and less than 10")
        if self.load_factor >= 10:
            raise Exception("Value for --lf must be less than 10")
        if self.service_provider_name is None:
            raise Exception("No value for --sp provided")
        if self.test_scenario_name is None:
            raise Exception("No test scenario specified")
        elif self.test_scenario_name not in self.__class__.test_scenarios:
            raise Exception("Invalid test scenario specified: {0}".format(self.test_scenario_name))
        

    def get_target(self, config):
        service_providers = config.get('service_providers')
        if not service_providers or not isinstance(service_providers, dict):
            raise Exception("'service_providers' key missing or invalid in configuration file")
        service_provider_config = service_providers.get(self.service_provider_name)
        if not service_provider_config or not isinstance(service_provider_config, dict):
            raise Exception("Service provider '{0}' missing or invalid".format(self.service_provider_name))
        service_provider_data = service_provider_config.get('data')
        if not service_provider_data or not isinstance(service_provider_data, dict):
            raise Exception("Missing or invalid 'data' configuration for service provider '{0}'".format(self.service_provider_name))
        url = service_provider_data.get('url')
        if not isinstance(url, str):
            raise Exception("Missing or invalid URL for service provider '{0}'")
        if url.startswith("https://catalogue.dataspace.copernicus.eu"):
            return cdse.CopernicusDataspaceConnector(service_provider_config)
    
    def run_test(self):
        with open(self.config_file_name, 'r') as stream:
            try:
                config = yaml.safe_load(stream)
                stream.close()
            except yaml.YAMLError as e:
                raise Exception("Error during loading of configuration file: {0}".format(str(e)))

        target = self.get_target(config)

        test_scenario = self.__class__.test_scenarios[self.test_scenario_name](
            config,
            target,
            self.load_factor
        )
        test_scenario.run_tests()
        result = test_scenario.generate_result()
        with open("{0}Results.json".format(test_scenario.id), 'w') as f:
            print(json.dumps(result, indent=2), file=f)
        

    
    @classmethod
    def print_usage(cls):
        print("""Usage: pycdab [-v] --conf=<config-file> --sp=<service-provider> <test-scenario> [--lf=<load-factor>] 
              
Test scenarios: {0}""".format(' | '.join(cls.test_scenarios)), file=sys.stderr)


def main():
    try:
        client = PyCdab(sys.argv)
    except Exception as e:
        print("ERROR: {0}".format(str(e)), file=sys.stderr)
        PyCdab.print_usage()
        sys.exit(1)

    try:
        client.run_test()
    except Exception as e:
        logger.error(str(e))
        sys.exit(2)

        

if __name__ == '__main__':
    main()