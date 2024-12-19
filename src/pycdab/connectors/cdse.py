import datetime
import re
import requests
from loguru import logger

class CopernicusDataspaceConnector:

    time_regex = re.compile(r"\[ *NOW(?P<op>[\+|\-])(?P<num>\d+)(?P<unit>[DMY])\]")
    credentials_regex = re.compile(r"(?P<username>[^:]+):(?P<password>.*)")

    def __init__(self, config, base_url = "https://catalogue.dataspace.copernicus.eu/odata/v1/Products"):
        self.config = config
        self.base_url = base_url
        self.access_token = None

    def query(self, thread_name, parameters, measurements):
        collection = None
        filter = None
        for key, param in parameters.items():
            value = param['value'].upper()

            condition = None

            if key == 'missionName':
                if value.upper().startswith("SENTINEL-5"):
                    value = "Sentinel-5P"
                collection = value
                condition = "Collection/Name eq '{0}'".format(collection)
            elif key == 'productType':
                condition = self.get_filter_condition("StringAttribute", "productType", value)
            elif key == 'processingLevel':
                condition = self.get_filter_condition("StringAttribute", "processingLevel", value)
            elif key == 'processingMode':
                condition = self.get_filter_condition("StringAttribute", "processingMode", value)
            elif key == 'polarisationChannels' or key == 'polarizationChannels':
                condition = self.get_filter_condition("StringAttribute", "polarisationChannels", value)
            elif key == 'track':
                condition = self.get_filter_condition("IntegerAttribute", "relativeOrbitNumber", value)
            elif key == 'sensorMode':
                condition = self.get_filter_condition("StringAttribute", "operationalMode", value)
            elif key == 'archiveStatus':
                condition = "{0}Online".format("" if value.lower() == "online" else "not ")
            elif key == 'sensingStart':
                value = self.transform_datetime(value)
                condition = "ContentDate/End gt {0}".format(value)
            elif key == 'sensingEnd':
                value = self.transform_datetime(value)
                condition = "ContentDate/Start lt {0}".format(value)
            elif key == 'geom':
                condition = "OData.CSC.Intersects(area=geography'SRID=4326;{0}')".format(value)

            if condition:
                if filter is None:
                    filter = ""
                else:
                    filter += " and "
                filter += condition

        logger.info("[{0}] Filter: {1}".format(thread_name, filter))

        measurements['start_time'] = datetime.datetime.now(datetime.timezone.utc)
        response = requests.get(self.base_url, params={'$filter': filter, '$expand': 'Attributes'})
        status_code = response.status_code
        measurements['end_time'] = datetime.datetime.now(datetime.timezone.utc)
        measurements['success'] = status_code == 200

        if response.status_code != 200:
            return {}
        
        measurements['size'] = int(response.headers.get('Content-Length'))
        measurements['total_results'] = -1   # total results number can be retrieved with $size=true, but is very slow on CDSE, therefore omitted

        result = response.json()
        items = result['value']
        measurements['read_results'] = len(items)

        result_errors = 0
        
        for item in items:
            logger.debug("[{0}] {1}".format(thread_name, item['Name']))
            
            # The check_result method is not implemented and returns always True (i.e. item matches query criteria)
            if not self.check_result(item, parameters):
                result_errors += 1

        measurements['result_errors'] = result_errors

        return items


    def transform_datetime(self, value):
        match = self.__class__.time_regex.match(value)
        now = datetime.datetime.now(datetime.timezone.utc)
        new_date = now
        delta = None
        if match:
            unit = match.group('unit')
            num = int(match.group('num'))
            op = match.group('op')
            if unit == "D":
                delta = datetime.timedelta(days=num)
            elif unit == "M":
                if num % 12 == 0:
                    new_year = now.year + (1 if op == "+" else -1) * num // 12
                    is_leap_year = new_year % 4 == 0 and (new_year % 100 != 0 or new_year % 400 == 0)
                    new_date = datetime.datetime(
                        year=new_year,
                        month=now.month, 
                        day=28 if not is_leap_year and now.month == 2 and now.day == 29 else now.day,
                        hour=now.hour,
                        minute=now.minute,
                        second=now.second,
                        microsecond=now.microsecond
                    )
                else:
                    delta = datetime.timedelta(days=num*30)
            elif unit == "Y":
                new_year = now.year + (1 if op == "+" else -1) * num
                is_leap_year = new_year % 4 == 0 and (new_year % 100 != 0 or new_year % 400 == 0)
                new_date = datetime.datetime(
                    year=new_year,
                    month=now.month, 
                    day=28 if not is_leap_year and now.month == 2 and now.day == 29 else now.day,
                    hour=now.hour,
                    minute=now.minute,
                    second=now.second,
                    microsecond=now.microsecond
                )
            
            if delta:
                if op == "+":
                    new_date += delta
                else:
                    new_date -= delta
            
            value = new_date.strftime("%Y-%m-%dT%H:%M:%SZ")

        return value

    def get_filter_condition(self, attr_type, name, value):
        quote = '\'' if attr_type == 'StringAttribute' else ''
        return "Attributes/OData.CSC.{0}/any(a:a/Name eq '{1}' and a/OData.CSC.{0}/Value eq {3}{2}{3})".format(attr_type, name, value, quote)


    def check_result(self, item, parameters):
        # Here the compliance of the result with the search criteria should be checked (for each item)
        # In CDSE this can be done using the Attributes values and the given parameters, not implemented here
        
        return True

    
    def download(self, thread_name, item, measurements):
        self.get_access_token()
        url = "{0}({1})/$value".format(self.base_url, item['Id'])
        logger.info("[{0}] Download URL: {1}".format(thread_name, url))
        redirect = True
        measurements['start_time'] = datetime.datetime.now(datetime.timezone.utc)
        while redirect:
            response = requests.get(url, headers={'Authorization': "Bearer {0}".format(self.access_token)}, stream=True, allow_redirects=False)
            redirect = response.headers.get('Location')
            if redirect:
                url = redirect
        expected_size = int(response.headers.get('Content-Length'))
        logger.info("[{0}] Download size: {1}".format(thread_name, expected_size))
        downloaded_size = 0
        last_10_mb = 0
        size_10_mb = 10485760
        for chunk in response.iter_content(chunk_size=1048576):
            downloaded_size += len(chunk)
            new_10_mb = downloaded_size // size_10_mb
            if new_10_mb != last_10_mb:
                logger.debug("[{0}] Downloaded: {1} bytes ({2}%)".format(thread_name, downloaded_size, round(100 * downloaded_size / expected_size, 2)))

        measurements['end_time'] = datetime.datetime.now(datetime.timezone.utc)
        measurements['success'] = response.status_code < 400 and downloaded_size == expected_size
        measurements['size'] = downloaded_size
        measurements['data_access'] = "http"
        measurements['data_collection_division'] = "{0} starting at {1} ending at {2}".format(item['Name'], measurements['start_time'].strftime("%Y-%m-%dT%H:%M:%SZ"), measurements['end_time'].strftime("%Y-%m-%dT%H:%M:%SZ"))

        logger.debug("[{0}] Download finished: {1} bytes".format(thread_name, downloaded_size))
            


    def get_access_token(self):
        if self.access_token:
            logger.debug("[{0}] Access token already exists")
            return
        match = self.__class__.credentials_regex.match(self.config['data']['credentials'])
        username = match.group('username')
        password = match.group('password')
        response = requests.post("https://identity.dataspace.copernicus.eu/auth/realms/CDSE/protocol/openid-connect/token", data={'grant_type': 'password', 'username': username, 'password': password, 'client_id': "cdse-public"})
        response.raise_for_status()

        content = response.json()
        self.access_token = content['access_token']
        logger.debug("[{0}] Access token obtained")



