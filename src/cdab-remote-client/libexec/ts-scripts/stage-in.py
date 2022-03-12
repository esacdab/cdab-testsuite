from __future__ import print_function
import os
import sys
import shutil
import subprocess
import re
import zipfile
import requests
import json


product_types = {
    'OL_2_LFR___': {
        'regex': re.compile(r'^(?P<id>S3[AB]_OL_2_LFR____(?P<yyyy>\d{4})(?P<mm>\d{2})(?P<dd>\d{2}).*_\d{3})(\.SEN3)?'),   # e.g.S3A_OL_2_LFR____20210214T094829_20210214T095129_20210215T150946_0179_068_250_2700_LN1_O_NT_002
        'creo_folder': 'Sentinel3'
    },
    'SL_2_LST___': {
        'regex': re.compile(r'^(?P<id>S3[AB]_SL_2_LST____(?P<yyyy>\d{4})(?P<mm>\d{2})(?P<dd>\d{2}).*_\d{3})(\.SEN3)?'),    # e.g.S3B_SL_2_LST____20210425T102904_20210425T103204_20210426T210428_0179_051_336_2340_LN2_O_NT_004
        'creo_folder': 'Sentinel3'
    }
}
credentials_regex = re.compile(r'((?P<username>[^:]+):)?(?P<password>.*)')


def download_from_provider():

    if provider == 'CREO':
        # Use /eodata folder (available with certain accounts/projects and VM configurations)

        if product_type == 'OL_2_LFR___':
            source = "/eodata/Sentinel-3/OLCI/OL_2_LFR/{0}/{1}/{2}/{3}.SEN3".format(
                product_yyyy,
                product_mm,
                product_dd,
                product_id
            )
            copy_tree(source, "{0}/{1}.SEN3".format(input_dir, product_id), "xfdumanifest.xml")

        elif product_type == 'SL_2_LST___':
            source = "/eodata/Sentinel-3/SLSTR/SL_2_LST/{0}/{1}/{2}/{3}.SEN3".format(
                product_yyyy,
                product_mm,
                product_dd,
                product_id
            )
            copy_tree(source, "{0}/{1}.SEN3".format(input_dir, product_id), "xfdumanifest.xml")
        else:
            raise Exception("No matching type found")

    elif provider == 'MUNDI': 
        # Use S3 protocol (via previously installed tool s3cmd)

        s3cmd_location = "/opt/anaconda/bin/s3cmd"

        if product_type == 'OL_2_LFR___':
            source = "s3://s3-olci/LFR/{0}/{1}/{2}/{3}.zip".format(
                product_yyyy,
                product_mm,
                product_dd,
                product_id
            )
            command = [s3cmd_location, 'get', source, input_dir]
            proc = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            proc.communicate()

            destination = "{0}/{1}.zip".format(input_dir, product_id)

            with zipfile.ZipFile(destination, 'r') as zip_ref:
                zip_ref.extractall(input_dir)
            os.remove(destination)

        elif product_type == 'SL_2_LST___':
            raise Exception("Type not provided by MUNDI")

        else:
            raise Exception("No matching type found")


    elif provider == 'ONDA':
        # Use NFS-linked directory (manually linked previously)

        if item_type == 'id':
            if product_type == 'OL_2_LFR___':
                source_base_1 = "/eodata/S3/OLCI/LEVEL-2/OL_2_LFR___/{0}/{1}/{2}/{3}.zip"
                source_base_2 = "/eodata/S3/OLCI/LEVEL-2/OL_2_LFR___/{0}/{1}/{2}/{3}.zip/{3}.SEN3"
            elif product_type == 'SL_2_LST___':
                source_base_1 = "/eodata/S3/SLSTR/LEVEL-2/SL_2_LST___/{0}/{1}/{2}/{3}.zip"
                source_base_2 = "/eodata/S3/SLSTR/LEVEL-2/SL_2_LST___/{0}/{1}/{2}/{3}.zip/{3}.SEN3"
            else:
                raise Exception("No matching type found")
                
            source = source_base_1.format(
                product_yyyy,
                product_mm,
                product_dd,
                product_id
            )
            if os.path.isfile(source):
                destination = "{0}/{1}.zip".format(input_dir, product_id)

                shutil.copy(source, destination)

                with zipfile.ZipFile(destination, 'r') as zip_ref:
                    zip_ref.extractall(input_dir)
                os.remove(destination)

            elif os.path.isdir(source):
                source = source_base_2.format(
                    product_yyyy,
                    product_mm,
                    product_dd,
                    product_id
                )
                copy_tree(source, "{0}/{1}.SEN3".format(input_dir, product_id), "xfdumanifest.xml")
            
            else:
                raise Exception("Product not found at {0}".format(source))

        else:
            r = requests.get(input_id, auth=(credentials_match.group('username'), credentials_match.group('password')), stream=True)
            destination = "{0}/tmp.zip".format(input_dir)
            with open(destination, 'wb') as f:
                for chunk in r.iter_content(chunk_size=8192): 
                    f.write(chunk)
            r.close()
            with zipfile.ZipFile(destination, 'r') as zip_ref:
                zip_ref.extractall(input_dir)
            os.remove(destination)

    elif provider == 'SOBLOO':
        # Use DirectData API (with API key, passed as the credentials password)

        sobloo_api_key = credentials_match.group('password')
        if product_type == 'OL_2_LFR___' or product_type == 'SL_2_LST___':
            url_regex = re.compile(r'.*\.SEN3(/(?P<dir>[^\?]*))?(/(?P<file>[^/\?]+))(\?.*)?')

            response = requests.post("https://sobloo.eu/api/v1-beta/direct-data/product-links",
                headers = {'Authorization': "Apikey {0}".format(sobloo_api_key)},
                json={'product': product_id, 'regexp': ".*"}
            )
            result = json.loads(response.text)
            
            download_list = [ l['url'] for l in result['links'] ]
            for url in download_list:
                m = url_regex.match(url)
                if not m:
                    raise('Unrecognised URL pattern: {0}'.format(url))

                file_dir = "{0}.SEN3/{1}{2}".format(product_id, '/' if m.group('dir') else '', m.group('dir') if m.group('dir') else '')
                file_name = m.group('file')

                complete_dir = "{0}/{1}".format(input_dir, file_dir)
                if not os.path.isdir(complete_dir):
                    os.makedirs(complete_dir)

                location = "{0}/{1}".format(complete_dir, file_name)
                r = requests.get(url, stream=True)
                with open(location, 'wb') as f:
                    for chunk in r.iter_content(chunk_size=8192): 
                        f.write(chunk)
                r.close()

        else:
            raise Exception("No matching type found")



def download_data_via_creo(collection_folder):
    global creo_token

    destination_path = input_dir
    if not os.path.isdir(destination_path):
        os.makedirs(destination_path)

    # Get metadata from CREODIAS catalogue
    query_url = "https://finder.creodias.eu/resto/api/collections/{0}/search.json?productIdentifier=%25{1}%25".format(collection_folder, product_id)
    print('Query CREODIAS catalogue: {0}'.format(query_url), file=sys.stderr)
    response = requests.get(query_url)
    response_json = response.json()

    # Extract URL from response
    download_url = None
    if 'features' in response_json and len(response_json['features']) != 0:
        feature = response_json['features'][0]
        if 'properties' in feature:
            properties = feature['properties']
        if 'services' in properties and 'download' in properties['services'] and 'url' in properties['services']['download']:
            download_url = properties['services']['download']['url']

    if not download_url:
        raise Exception("Product not found: {0}".format(product_id))

    print('Download URL: {0}'.format(download_url), file=sys.stderr)

    # Download file (if no download token is available or first attempt fails with status 4XX, renew token)
    retry_count = 0
    max_retries = 2
    while retry_count < max_retries:
        success = False
        retry_count += 1

        if not creo_token or retry_count == 2:
            creo_token = login_on_creodias()

        response = requests.get(download_url, params={'token': creo_token}, stream=True)

        if response.status_code == 200:
            success = True
        if response.status_code >= 400 and response.status_code < 500:
            if retry_count < max_retries:
                continue
            else:
                raise Exception("Cannot download {0} from {1}".format(product_id, download_url))

        zip_file = "{0}/{1}.zip".format(destination_path, product_id)
        with open(zip_file, 'wb') as f:
            for chunk in response.iter_content(chunk_size=8192): 
                f.write(chunk)
        response.close()

        with zipfile.ZipFile(zip_file, 'r') as zip_ref:
            zip_ref.extractall(destination_path)

        os.remove(zip_file)

        if success:
            print('Product successfully downloaded from CREODIAS: {0}'.format(product_id), file=sys.stderr)
            break

    return os.path.abspath(destination_path)



def login_on_creodias():
    print('Get CREODIAS token', file=sys.stderr)

    credentials_match = credentials_regex.match(creo_credentials)
    username = credentials_match.group('username')
    password = credentials_match.group('password')

    if not password:
        password = ''

    response = requests.post(
        "https://auth.creodias.eu/auth/realms/DIAS/protocol/openid-connect/token", 
        data={'client_id': 'CLOUDFERRO_PUBLIC', 'username': username, 'password': password, 'grant_type': 'password'},
        json=True
    )

    response_json = response.json()
    if 'access_token' in response_json:
        print('Received CREODIAS token', file=sys.stderr)
        return response_json['access_token']

    raise Exception("No access token")


def copy_tree(source, destination, file_check=None):
    if file_check:
        if not os.path.isfile("{0}/{1}".format(source, file_check)):
            raise Exception("Product not found at {0}".format(source))

    if os.path.exists(destination):
        return

    shutil.copytree(source, destination)



if len(sys.argv) < 4:
    print("Usage: stage-in.py <item-type> <product-type> <provider> <input-id> <input-dir>", file=sys.stderr)
    sys.exit(1)

product_type = None

item_type = sys.argv[1]  # id or url
product_type = sys.argv[2]
provider = sys.argv[3]
input_id = sys.argv[4]
input_dir = sys.argv[5]
credentials = sys.argv[6]
if len(sys.argv) >= 8:
    creo_credentials = sys.argv[7]
else:
    creo_credentials = None
# Find matching product type from product identifier

product_type_match = None
if product_type is None:
    for p in product_types:
        product_type_match = product_types[p]['regex'].match(input_id)
        if product_type_match:
            product_type = p
            break
else:
    product_type_match = product_types[product_type]['regex'].match(input_id)

if product_type_match:
    product_id = product_type_match.group('id')
    product_yyyy = product_type_match.group('yyyy')
    product_mm = product_type_match.group('mm')
    product_dd = product_type_match.group('dd')

credentials_match = credentials_regex.match(credentials)

try:
    download_from_provider()

except Exception as e:
    print("ERROR during stage in of {0}: {1}".format(input_id, str(e)), file=sys.stderr)

    if creo_credentials:
        creo_token = None
        try:
            download_data_via_creo(product_types[product_type]['creo_folder'])
        except:
            print("ERROR during stage in (alternative download) of {0}: {1}".format(input_id, str(e)), file=sys.stderr)
            sys.exit(1)

    else:
        sys.exit(1)
