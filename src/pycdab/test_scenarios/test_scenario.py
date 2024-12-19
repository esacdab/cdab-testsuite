from loguru import logger
from test_cases import test_case



class TestScenario:

    def __init__(self, id, title, config, target, load_factor):
        self.id = id
        self.title = title
        self.config = config
        self.target = target
        self.load_factor = load_factor
        self.test_cases = []
        pass

    def run_tests(self):
        for test_case in self.test_cases:
            test_case.prepare()
            test_case.run()

    def generate_result(self):
        result = {
            
        }
        test_case_results = []
        for test_case in self.test_cases:
            test_case_results.append(test_case.generate_result())
        result['testCaseResults'] = test_case_results
        return result



class TestScenario01(TestScenario):

    def __init__(self, config, target, load_factor):
        super().__init__("TS01", "Simple data search and single download", config, target, load_factor)
        self.all_query_results = []
        self.test_cases.append(test_case.TestCase101(self))
        self.test_cases.append(test_case.TestCase201(self))
        self.test_cases.append(test_case.TestCase301(self))



class TestScenario02(TestScenario):

    def __init__(self, config, target, load_factor):
        super().__init__("TS02", "Complex data search and bulk download", config, target, load_factor)
        self.all_query_results = []
        self.test_cases.append(test_case.TestCase202(self))
        self.test_cases.append(test_case.TestCase302(self))
