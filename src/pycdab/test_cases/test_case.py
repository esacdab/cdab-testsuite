import threading
import time
import datetime
from loguru import logger
import requests
import json
import random
from test_cases import missions

class TestCase:

    def __init__(self, id, title, scenario):
        self.id = id
        self.title = title
        self.scenario = scenario
        self.threads = []
        self.measurements = []
        for i in range(self.scenario.load_factor):
            self.measurements.append({'success': False})

    def create_threads(self):
        for i in range(self.scenario.load_factor):
            self.threads.append(threading.Thread(target=self.run_single, args=(i, "Thread #{0}".format(i + 1))))

    def prepare(self):
        logger.info("Preparing test {0} ({1})".format(self.id, self.title))
        pass

    def run(self):
        self.start_time = datetime.datetime.now(datetime.timezone.utc)
        for thread in self.threads:
            thread.start()
        for thread in self.threads:
            thread.join()
        self.end_time = datetime.datetime.now(datetime.timezone.utc)


    def run_single(self, index, name):
        pass

    def generate_basic_result(self):
        return {
            'testName': self.id,
            'className': self.__class__.__name__,
            'startedAt': self.start_time.strftime("%Y-%m-%dT%H:%M:%S.%fZ"),
            'endedAt': self.end_time.strftime("%Y-%m-%dT%H:%M:%S.%fZ"),
            'duration': round((self.end_time - self.start_time).total_seconds() * 1000),
        }
    
    def generate_basic_metrics(self, measurement_values):
        if measurement_values:
            total_response_ms = round(sum(((m['end_time'] - m['start_time']).total_seconds() * 1000 for m in measurement_values)))
            max_response_ms = max(((m['end_time'] - m['start_time']).total_seconds() * 1000 for m in measurement_values))
            parallel_duration_ms = round((max((m['end_time'] for m in self.measurements if m['success'])) - min((m['start_time'] for m in measurement_values))).total_seconds() * 1000)
            peak_concurrency = self.get_peak_concurrency(((m['start_time'] for m in measurement_values)), ((m['end_time'] for m in measurement_values)))

        return [
            {
                "name": "avgResponseTime",
                "value": round(total_response_ms / len(measurement_values)) if measurement_values else -1,
                "uom": "ms"
            },
            {
                "name": "peakResponseTime",
                "value": round(max_response_ms) if measurement_values else -1,
                "uom": "ms"
            },
            {
                "name": "errorRate",
                "value": round(1 - len(measurement_values) / self.scenario.load_factor, 3),
                "uom": "%"
            },
            {
                "name": "avgConcurrency",
                "value": round(total_response_ms / parallel_duration_ms, 3) if measurement_values else -1,
                "uom": "#"
            },
            {
                "name": "peakConcurrency",
                "value": peak_concurrency if measurement_values else -1,
                "uom": "#"
            }
        ]
    
    def generate_query_metrics(self, measurement_values):
        if measurement_values:
            total_size = sum(((m['size'] for m in measurement_values)))
            max_size = max(((m['size'] for m in measurement_values)))
            total_read_results = sum(((m['read_results'] for m in measurement_values)))
            total_result_errors = sum(((m['result_errors'] for m in measurement_values)))
            max_total_results = max(((m['total_results'] for m in measurement_values)))

        return [
            {
                "name": "avgSize",
                "value": round(total_size / len(measurement_values)) if measurement_values else -1,
                "uom": "bytes"
            },
            {
                "name": "maxSize",
                "value": max_size if measurement_values else -1,
                "uom": "bytes"
            },
            {
                "name": "totalSize",
                "value": total_size if measurement_values else -1,
                "uom": "bytes"
            },
            {
                "name": "totalReadResults",
                "value": total_read_results if measurement_values else -1,
                "uom": "#"
            },
            {
                "name": "resultsErrorRate",
                "value": round(total_result_errors / total_read_results, 3) if measurement_values else -1,
                "uom": "%"
            },
            {
                "name": "maxTotalResults",
                "value": max_total_results if measurement_values else -1,
                "uom": "#"
            },
            {
                "name": "dataCollectionDivision",
                "value": [
                    "Sentinel-5P L2 NRT last 1M Online"
                ],
                "uom": "string"
            }
        ]
    

    def generate_download_metrics(self, measurement_values):
        if measurement_values:
            total_size = sum(((m['size'] for m in measurement_values)))
            max_size = max(((m['size'] for m in measurement_values)))
            total_response_s = sum(((m['end_time'] - m['start_time']).total_seconds() for m in measurement_values))

        return [
            {
                "name": "avgSize",
                "value": round(total_size / len(measurement_values)) if measurement_values else -1,
                "uom": "bytes"
            },
            {
                "name": "maxSize",
                "value": max_size if measurement_values else -1,
                "uom": "bytes"
            },
            {
                "name": "totalSize",
                "value": total_size if measurement_values else -1,
                "uom": "bytes"
            },
            {
                "name": "throughput",
                "value": round(total_size / total_response_s, 3) if measurement_values else -1,
                "uom": "bytes/second"
            },
            {
                "name": "dataCollectionDivision",
                "value": [m['data_collection_division'] for m in measurement_values],
                "uom": "string"
                },
            {
                "name": "dataAccess",
                "value": [m['data_access'] for m in measurement_values],
                "uom": "string"
            }
        ]


    
    def generate_result(self):
        result = self.generate_basic_result()
        return result
    
    def get_peak_concurrency(self, start_times, end_times):
        # Order start and end times in one list and find maximum concurrency
        times = []
        times.extend(((t, 1) for t in start_times))
        times.extend(((t, -1) for t in end_times))
        times.sort(key=lambda t: t[0])
        peak = 0
        conc = 0
        for t in times:
            conc += t[1]   # add 1 if a process started, subtract 1 if a process ended
            if conc > peak: peak = conc
        return peak




class TestCase101(TestCase):

    def __init__(self, scenario):
        super().__init__("TC101", "Service reachability", scenario)

    def prepare(self):
        super().prepare()
        self.url = self.scenario.target.config['data']['url']
        self.create_threads()
    
    def run_single(self, index, name):
        logger.info("{0} {1} start".format(self.id, name))
        self.measurements[index]['start_time'] = datetime.datetime.now(datetime.timezone.utc)
        response = requests.get(self.url)
        status_code = response.status_code
        self.measurements[index]['end_time'] = datetime.datetime.now(datetime.timezone.utc)
        self.measurements[index]['success'] = status_code < 400
        logger.info("{0} {1} end".format(self.id, name))

    
    def generate_result(self):
        successful_measurements = [m for m in self.measurements if m['success']]
        result = super().generate_result()
        result['metrics'] = self.generate_basic_metrics(successful_measurements)
        return result
    



class TestCase201(TestCase):
    
    def __init__(self, scenario):
        super().__init__("TC201", "Basic catalogue query", scenario)

    def prepare(self):
        super().prepare()

        self.queries = []

        for i in range(self.scenario.load_factor):
            set = random.choice(list(self.scenario.config['data']['sets'].values()))
            self.queries.append(set['parameters'])

        self.create_threads()

    def run_single(self, index, name):
        logger.info("{0} {1} start".format(self.id, name))
        target = self.scenario.target
        query_result = target.query(name, self.queries[index], self.measurements[index])
        self.scenario.all_query_results.extend(query_result)
        logger.info("{0} {1} end".format(self.id, name))


    def generate_result(self):
        successful_measurements = [m for m in self.measurements if m['success']]

        result = super().generate_result()
        result['metrics'] = self.generate_basic_metrics(successful_measurements)
        result['metrics'].extend(self.generate_query_metrics(successful_measurements))

        return result


class TestCase202(TestCase):

    def __init__(self, scenario):
        super().__init__("TC202", "Complex query", scenario)
     
    def prepare(self):
        super().prepare()

        self.queries = []

        for i in range(self.scenario.load_factor):
            set = random.choice(list(self.scenario.config['data']['sets'].values()))

            # Add random area
            geom_key = random.choice(list(missions.geometries))
            set['parameters']['geom'] = missions.geometries[geom_key]

            # Add random date range
            mission_dict = set['parameters'].get('missionName')
            if mission_dict:
                mission = mission_dict['value']
                if mission.lower().startswith("sentinel-5"):
                    mission = "Sentinel-5P"
                mission_parameters = missions.get_missions_parameters().get(mission.lower())
                if mission_parameters:
                    min_time = datetime.datetime.strptime(mission_parameters['lifetimeStart'], "%Y-%m-%dT%H:%M:%SZ").astimezone(datetime.timezone.utc)
                    max_time = datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(days=1)
                    total_days = int((max_time - min_time).days)
                    start_day_number = random.randint(0, total_days)
                    end_day_number = random.randint(start_day_number, total_days)
                    start_time = min_time + datetime.timedelta(days=start_day_number)
                    end_time = min_time + datetime.timedelta(days=end_day_number)

                    set['parameters']['sensingStart'] = {
                        'label': "from {0}".format(start_time.strftime("%Y-%m-%dT%H:%M:%SZ")),
                        'value': start_time.strftime("%Y-%m-%dT%H:%M:%SZ"),
                    }
                    set['parameters']['sensingEnd'] = {
                        'label': "from {0}".format(end_time.strftime("%Y-%m-%dT%H:%M:%SZ")),
                        'value': end_time.strftime("%Y-%m-%dT%H:%M:%SZ"),
                    }


            self.queries.append(set['parameters'])

        self.create_threads()

    def run_single(self, index, name):
        logger.info("{0} {1} start".format(self.id, name))
        target = self.scenario.target
        query_result = target.query(name, self.queries[index], self.measurements[index])
        self.scenario.all_query_results.extend(query_result)
        logger.info("{0} {1} end".format(self.id, name))


    def generate_result(self):
        successful_measurements = [m for m in self.measurements if m['success']]

        result = super().generate_result()
        result['metrics'] = self.generate_basic_metrics(successful_measurements)
        result['metrics'].extend(self.generate_query_metrics(successful_measurements))

        return result



class TestCase301(TestCase):

    def __init__(self, scenario):
        super().__init__("TC301", "Single remote download", scenario)

    def prepare(self):
        super().prepare()
        logger.debug("Selecting random item from results of previous test case")
        if self.scenario.all_query_results:
            self.item = random.choice(self.scenario.all_query_results)
            logger.info("Item for download: {0}".format(self.item["Name"]))
            self.threads.append(threading.Thread(target=self.run_single, args=(0, "Download thread")))

        else:
            logger.warning("No item available for download")


    def run_single(self, index, name):
        logger.info("{0} {1} start".format(self.id, name))
        target = self.scenario.target
        target.download(name, self.item, self.measurements[index])
        logger.info("{0} {1} end".format(self.id, name))


    def generate_result(self):
        successful_measurements = [m for m in self.measurements if m['success']]

        result = super().generate_result()
        result['metrics'] = self.generate_basic_metrics(successful_measurements)
        result['metrics'].extend(self.generate_download_metrics(successful_measurements))

        return result



class TestCase302(TestCase):

    def __init__(self, scenario):
        super().__init__("TC302", "Multiple remote download", scenario)

    def prepare(self):
        super().prepare()
        self.items = []
        logger.debug("Selecting random item from results of previous test case")
        if self.scenario.all_query_results:
            for i in range(self.scenario.load_factor):
                random_item = random.choice(self.scenario.all_query_results)
                self.items.append(random_item)
                logger.info("Item for download ({0}): {1}".format(i + 1, random_item["Name"]))

            self.create_threads()

        else:
            logger.warning("No item available for download")


    def run_single(self, index, name):
        logger.info("{0} {1} start".format(self.id, name))
        target = self.scenario.target
        target.download(name, self.items[index], self.measurements[index])
        logger.info("{0} {1} end".format(self.id, name))


    def generate_result(self):
        successful_measurements = [m for m in self.measurements if m['success']]

        result = super().generate_result()
        result['metrics'] = self.generate_basic_metrics(successful_measurements)
        result['metrics'].extend(self.generate_download_metrics(successful_measurements))

        return result
