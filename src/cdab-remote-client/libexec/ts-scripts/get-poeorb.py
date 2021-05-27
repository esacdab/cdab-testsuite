from __future__ import print_function
import sys
import os
import zipfile
import datetime
import requests
import json

# Orbits (<env-base>/snap/.snap/auxdata/Orbits) should contain a structure like this:
# `-- Sentinel-1
#     `-- POEORB
#         `-- S1A
#             `-- 2020
#                 |-- 06
#                 |   `-- S1A_OPER_AUX_POEORB_OPOD_20210319T032445_V20200627T225942_20200629T005942.EOF.zip
#                 `-- 08
#                     `-- S1A_OPER_AUX_POEORB_OPOD_20210317T062946_V20200814T225942_20200816T005942.EOF.zip


def get_search_result(platform, start_time, end_time):

    q = "platformname:Sentinel-1 AND platformnumber:{0} AND producttype:AUX_POEORB AND beginposition:[2000-01-01T00:00:00Z TO {1}] AND endposition:[{2} TO 2100-01-01T00:00:00Z]".format(
        platform[2:3],   # A or B
        end_time.strftime('%Y-%m-%dT%H:%M:%S.%fZ'),
        start_time.strftime('%Y-%m-%dT%H:%M:%S.%fZ')
    )
    params = {'q': q, 'rows': 20, 'format': 'json'}

    response = requests.get("https://scihub.copernicus.eu/gnss/search", auth=('gnssguest', 'gnssguest'), params=params)

    result = json.loads(response.text)

    return result


def download(entry, path):

    if 'str' in entry and 'date' in entry:
        for field in entry['str'] + entry['date']:
            name = field['name']
            value = field['content']
            if name == 'identifier': identifier = value


    if 'link' in entry:
        download_url = next((l['href'] for l in entry['link'] if l['href'][-6:] == '$value'), None)

    if not identifier or not download_url:
        raise Exception('Incomplete metadata')

    print("Download of '{0}' from {1}".format(identifier, download_url), file=sys.stderr)

    aux_filename = '{0}/{1}.EOF'.format(path, identifier)
    aux_zip_filename = '{0}.zip'.format(aux_filename)

    if os.path.basename(aux_zip_filename) not in os.listdir(path):
        response = requests.get(download_url, auth=('gnssguest', 'gnssguest'), stream=True)
        with open(aux_filename, 'wb') as f:
            for chunk in response.iter_content(chunk_size=8192): 
                f.write(chunk)
        response.close()
        print("Orbit file {0} downloaded".format(os.path.basename(aux_filename)), file=sys.stderr)
    
        current_dir = os.curdir

        os.chdir(path)

        zipfile.ZipFile(os.path.basename(aux_zip_filename), mode='w').write(os.path.basename(aux_filename))
        os.remove(aux_filename)

        os.chdir(current_dir)
        print("Zipped orbit file created at {0}".format(aux_zip_filename), file=sys.stderr)

    else:
        print("Orbit file {0} already present".format(aux_filename), file=sys.stderr)
    

env_base_dir = sys.argv[1]

identifiers = []
for arg in sys.argv[2:]:
     identifiers.append(arg)

for identifier in identifiers:
    platform = identifier[0:3]
    start_time = datetime.datetime.strptime(identifier[17:32], '%Y%m%dT%H%M%S')
    end_time = datetime.datetime.strptime(identifier[33:48], '%Y%m%dT%H%M%S')
    product_date = start_time
    
    sub_path = "{0}/{1}".format(platform, datetime.datetime.strftime(product_date, '%Y/%m'))
    full_path = "{0}/snap/.snap/auxdata/Orbits/Sentinel-1/POEORB/{1}".format(
        env_base_dir,
        sub_path
    )

    if not os.path.isdir(full_path):
        os.makedirs(full_path)

    result = get_search_result(platform, start_time, end_time)

    if "feed" in result and "opensearch:totalResults" in result["feed"]:
        total_results = int(result["feed"]["opensearch:totalResults"])

    if "feed" in result and "entry" in result["feed"]:

        entries = result["feed"]["entry"]
        if not isinstance(entries, list):
            entries = [ entries ]

        for entry in entries:
            download(entry, full_path)


