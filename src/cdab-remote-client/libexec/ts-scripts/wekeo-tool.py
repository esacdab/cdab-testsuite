import sys
import base64
import json
import requests
import urllib
import datetime
import time
import re


t2_catalog_base_url = "https://catalog.terradue.com"
wekeo_hda_base_url = "https://wekeo-broker.apps.mercator.dpi.wekeo.eu/databroker"

key_value_regex = re.compile(r"--(?P<key>.+?)=(?P<value>.*)")
wkt_point_regex = re.compile(r"POINT\( *(?P<lon>[^ ]+) +(?P<lat>[^ ]+)\)")



def login(credentials):

    response = requests.get("{0}/gettoken".format(wekeo_hda_base_url),
        headers={'Authorization': "Basic {0}".format(base64.b64encode("{0}".format(credentials).encode('ascii')).decode('ascii'))}
    )
    return response.json()["access_token"]


def get_product_info(index, uid, bbox=None, dates=None):
    product_json = requests.get("{0}/{1}/search".format(t2_catalog_base_url, index), params={'uid': uid, 'format': 'json'}).json()
    if 'features' in product_json and len(product_json['features']) != 0:
        print("Found on catalogue: {0}".format(uid), file=sys.stderr)
        feature = product_json['features'][0]
        if bbox is None:
            bbox_str = feature['properties']['box']
            bbox = bbox_str.split(' ')
            if len(bbox) == 4:
                for i in range(4):
                    bbox[i] = float(bbox[i])
                bbox = "{0},{1},{2},{3}".format(
                    round((bbox[1] + bbox[3]) / 2 - 0.1, 2),
                    round((bbox[0] + bbox[2]) / 2 - 0.1, 2),
                    round((bbox[1] + bbox[3]) / 2 + 0.1, 2),
                    round((bbox[0] + bbox[2]) / 2 + 0.1, 2)
                )

        if dates is None:
            date_str = feature['properties']['date']
            dates = date_str.split('/')
            if len(dates) == 1:
                dates.append(dates[0])
            for i in range(2):
                dates[i] = datetime.datetime.strptime(dates[i][:19], '%Y-%m-%dT%H:%M:%S')
            mid_date = dates[0] + (dates[1] - dates[0]) / 2

            dates = "{0}/{1}".format(
                (mid_date - datetime.timedelta(seconds=delta)).strftime('%Y-%m-%dT%H:%M:%S'),
                (mid_date + datetime.timedelta(seconds=delta)).strftime('%Y-%m-%dT%H:%M:%S')
            )

    if bbox is None:
        bbox = "12.4,41.8,12.6,42.0"
    if dates is None:
        mid_date = datetime.datetime.utcnow() - datetime.timedelta(days=10)
        dates = "{0}/{1}".format(
            (mid_date - datetime.timedelta(hours=24)).strftime('%Y-%m-%dT%H:%M:%S'),
            (mid_date + datetime.timedelta(hours=24)).strftime('%Y-%m-%dT%H:%M:%S')
        )

    return {
        'bbox': bbox,
        'dates': dates
    }



def query(collection, parameters, uid=None):
    
    if collection is None:
        request = parameters
    else:
        request = {
            "datasetId": collection
        }
        
        # (1) Query (datarequest)
        for parameter in parameters:
            if parameter in ['cloud_coverage', 'cloudCoverage', 'missionTakeId', 'orbitnumber', 'rel_orbit_number', 'relativeOrbitNumber', 'relativeorbitnumber']:
                qp = { "name": parameter, "value": parameters[parameter] }
                if 'stringInputValues' not in request:
                    request['stringInputValues'] = []
                request['stringInputValues'].append(qp)
            elif parameter in ['mode', 'orbit_direction', 'orbitDirection', 'orbitdirection', 'platformname', 'polarisation', 'processingLevel', 'productType', 'producttype', 'sensorMode', 'swath', 'timeliness']:
                qp = { "name": parameter, "value": parameters[parameter] }
                if 'stringChoiceValues' not in request:
                    request['stringChoiceValues'] = []
                request['stringChoiceValues'].append(qp)
            elif parameter in ['dtrange', 'time', 'position']:
                dates = parameters[parameter].split('/')
                qp = { "name": parameter, "start": dates[0], "end": dates[1] }
                if 'dateRangeSelectValues' not in request:
                    request['dateRangeSelectValues'] = []
                request['dateRangeSelectValues'].append(qp)
            elif parameter in ['bbox']:
                qp = { "name": parameter, "bbox": [ float(c) for c in parameters[parameter].split(',') ] }
                if 'boundingBoxValues' not in request:
                    request['boundingBoxValues'] = []
                request['boundingBoxValues'].append(qp)

    print("Query to {0}/datarequest".format(wekeo_hda_base_url), file=sys.stderr)
    print(json.dumps(request, indent=2), file=sys.stderr)

    query_start_time = datetime.datetime.utcnow()
    response = requests.post("{0}/datarequest".format(wekeo_hda_base_url),
        headers={'Authorization': token},
        json=request,
    )
    query_end_time = datetime.datetime.utcnow()
    query_duration_seconds = round((query_end_time - query_start_time).total_seconds(), 3)

    print("Response time: {0} sec".format(query_duration_seconds), file=sys.stderr)

    print(json.dumps(response.json(), indent=2), file=sys.stderr)
    job_id = response.json()['jobId']

    time.sleep(1)

    # (2) Job status
    max_attempts = 25
    attempt_count = 0
    status = None
    while attempt_count < max_attempts:
        attempt_count += 1
        response = requests.get("{0}/datarequest/status/{1}".format(wekeo_hda_base_url, job_id),
            headers={'Authorization': token}
        )
        status = response.json()['status']
        print("Status: {0}".format(status), file=sys.stderr)

        if status == 'completed' or status == 'failed' or attempt_count == max_attempts:
            break
        
        time.sleep(10)

    urls = []
    if status == 'completed':
        response = requests.get("{0}/datarequest/jobs/{1}/result".format(wekeo_hda_base_url, job_id),
            headers={'Authorization': token},
            params={'size': '20'}
        )
        result = response.json()
        print(json.dumps(result, indent=2), file=sys.stderr)
        if 'content' in result and len(result['content']) != 0:
            for item in result['content']:
                if not uid or uid in item['productInfo']['product']:
                    urls.append("{0}#{1}".format(item['url'], job_id))
    elif status == 'failed':
        result = response.json()
        print(json.dumps(result, indent=2), file=sys.stderr)

    for url in urls[:count]:
        print(url)


def download(url, destination):
    s = url.split('#')
    uri = s[0]
    job_id = s[1]

    request = {
        'jobId': job_id,
        'uri': uri
    }

    print("Query to {0}/dataorder".format(wekeo_hda_base_url), file=sys.stderr)
    print(json.dumps(request, indent=2), file=sys.stderr)

    query_start_time = datetime.datetime.utcnow()
    response = requests.post("{0}/dataorder".format(wekeo_hda_base_url),
        headers={'Authorization': token},
        json=request,
    )
    query_end_time = datetime.datetime.utcnow()
    query_duration_seconds = round((query_end_time - query_start_time).total_seconds(), 3)

    print("Response time: {0} sec".format(query_duration_seconds), file=sys.stderr)

    print(json.dumps(response.json(), indent=2), file=sys.stderr)
    order_id = response.json()['orderId']

    time.sleep(1)

    # (2) Order status
    max_attempts = 25
    attempt_count = 0
    status = None
    while attempt_count < max_attempts:
        attempt_count += 1
        response = requests.get("{0}/dataorder/status/{1}".format(wekeo_hda_base_url, order_id),
            headers={'Authorization': token}
        )
        status = response.json()['status']
        print("Status: {0}".format(status), file=sys.stderr)

        if status == 'completed' or status == 'failed' or attempt_count == max_attempts:
            break
        
        time.sleep(10)

    if status == 'completed':
        download_url = "{0}/dataorder/download/{1}".format(wekeo_hda_base_url, order_id)
        print(download_url, file=sys.stderr)

        r = requests.head(download_url, allow_redirects=False)
        print(r.status_code)
        if r.status_code >= 300 and r.status_code < 400:   # Redirect
            download_url = r.headers['Location']
        r.close()
        print(download_url, file=sys.stderr)

        if download_url.startswith('ftp://'):
            urllib.request.urlretrieve(download_url, destination)
        else:
            r = requests.get(download_url, verify=False, stream=True)
            with open(destination, 'wb') as f:
                for chunk in r.iter_content(chunk_size=8192): 
                    f.write(chunk)
            r.close()




credentials = None
t2_index = None
uid = None
platform = None
product_type = None
bbox = None
dates = None
rel_orbit = None
url = None
destination = None
count=20

operation = sys.argv[1]
if operation not in ['query', 'download']:
    print("Operation not supported", file=sys.stderr)
    sys.exit(1)

for arg in sys.argv[2:]:
    match = key_value_regex.match(arg)
    if match:
        key = match.group('key')
        value = match.group('value')
        if key == 'credentials':
            credentials = value
        elif key == 'uid':
            uid = value
        elif key == 'pn':
            platform = value
        elif key == 'pt':
            product_type = value
        elif key == 'bbox':
            bbox = value
        elif key == 'geom':
            match = wkt_point_regex.match(value)
            if match:
                lon = float(match.group('lon'))
                lat = float(match.group('lat'))
                bbox = "{0},{1},{2},{3}".format(lon - 0.1, lat - 0.1, lon + 0.1, lat + 0.1)
        elif key == 'dates':
            dates = value
        elif key == 'relorbit':
            rel_orbit = value
        elif key == 'url':
            url = value
        elif key == 'dest':
            destination = value
        elif key == 'count':
            count = int(value)

token = login(credentials)

if sys.argv[1] == 'query':

    parameters = {}
    delta = 10

    if platform == 'Sentinel-1' and  product_type == "SLC":
        t2_index = 'sentinel1'
        collection = "EO:ESA:DAT:SENTINEL-1:SAR"
        parameters['productType'] = 'SLC'
        if rel_orbit:
            parameters['relativeOrbitNumber'] = rel_orbit
        delta = 60
    elif platform == 'Sentinel-2' and  product_type == "S2MSI1C":
        t2_index = 'sentinel2'
        collection = "EO:ESA:DAT:EODC-SENTINEL-2:MSI1C"
    elif platform == 'Sentinel-3' and  product_type == "OL_2_LFR___":
        t2_index = 'sentinel3'
        collection = "EO:ESA:DAT:SENTINEL-3:OL_2_LFR___"
        parameters['productType'] = 'LFR'
        delta = 150
    elif platform == 'Sentinel-3' and  product_type == "SL_2_LST___":
        t2_index = 'sentinel3'
        collection = "EO:ESA:DAT:SENTINEL-3:SL_2_LST___"
        parameters['productType'] = 'LST'
        delta = 150
    else:
        print("Search not supported", file=sys.stderr)
        sys.exit(1)

    if uid is None:
        if bbox:
            parameters['bbox'] = bbox
        if dates:
            parameters['dtrange'] = dates
    else:
        res = get_product_info(t2_index, uid, bbox, dates)
        parameters['bbox'] = res['bbox']
        parameters['dtrange'] = res['dates']
    
    query(
        collection,
        parameters,
        uid
    )

elif operation == 'download':
    download(url, destination)







