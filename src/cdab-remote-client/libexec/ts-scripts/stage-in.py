from __future__ import print_function
import os
import sys
import shutil
import subprocess
import re
import zipfile
import requests
import json

def copy_tree(source, destination, file_check=None):
    if file_check:
        if not os.path.isfile("{0}/{1}".format(source, file_check)):
            raise Exception("Product not found at {0}".format(source))

    if os.path.exists(destination):
        return

    shutil.copytree(source, destination)


product_types = {
    'OL_2_LFR___': re.compile(r'^(?P<id>S3[AB]_OL_2_LFR____(?P<yyyy>\d{4})(?P<mm>\d{2})(?P<dd>\d{2}).*_\d{3})(\.SEN3)?') # e.g.S3A_OL_2_LFR____20210214T094829_20210214T095129_20210215T150946_0179_068_250_2700_LN1_O_NT_002
}
credentials_regex = re.compile(r'((?P<username>[^:]+):)?(?P<password>.*)')

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

# Find matching product type from product identifier

product_type_match = None
if product_type is None:
    for p in product_types:
        product_type_match = product_types[p].match(input_id)
        if product_type_match:
            product_type = p
            break
else:
    product_type_match = product_types[product_type].match(input_id)

credentials_match = credentials_regex.match(credentials)

try:
    if provider == 'CREO':
        # Use /eodata folder (available with certain accounts/projects and VM configurations)

        if product_type == 'OL_2_LFR___':
            source = "/eodata/Sentinel-3/OLCI/OL_2_LFR/{0}/{1}/{2}/{3}.SEN3".format(
                product_type_match.group('yyyy'),
                product_type_match.group('mm'),
                product_type_match.group('dd'),
                product_type_match.group('id')
            )
            copy_tree(source, "{0}/{1}.SEN3".format(input_dir, product_type_match.group('id')), "xfdumanifest.xml")
        else:
            raise Exception("No matching type found")

    elif provider == 'MUNDI': 
        # Use S3 protocol (via previously installed tool s3cmd)

        s3cmd_location = "/opt/anaconda/bin/s3cmd"

        if product_type == 'OL_2_LFR___':
            source = "s3://s3-olci/LFR/{0}/{1}/{2}/{3}.zip".format(
                product_type_match.group('yyyy'),
                product_type_match.group('mm'),
                product_type_match.group('dd'),
                product_type_match.group('id')
            )
            command = [s3cmd_location, 'get', source, input_dir]
            proc = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            proc.communicate()

            destination = "{0}/{1}.zip".format(input_dir, product_type_match.group('id'))

            with zipfile.ZipFile(destination, 'r') as zip_ref:
                zip_ref.extractall(input_dir)
            os.remove(destination)
        else:
            raise Exception("No matching type found")


    elif provider == 'ONDA':
        # Use NFS-linked directory (manually linked previously)

        if product_type == 'OL_2_LFR___':
            if item_type == 'id':
                source = "/eodata/S3/OLCI/LEVEL-2/OL_2_LFR___/{0}/{1}/{2}/{3}.zip".format(
                    product_type_match.group('yyyy'),
                    product_type_match.group('mm'),
                    product_type_match.group('dd'),
                    product_type_match.group('id')
                )
                if os.path.isfile(source):
                    destination = "{0}/{1}.zip".format(input_dir, product_type_match.group('id'))

                    shutil.copy(source, destination)

                    with zipfile.ZipFile(destination, 'r') as zip_ref:
                        zip_ref.extractall(input_dir)
                    os.remove(destination)
                elif os.path.isdir(source):
                    source = "/eodata/S3/OLCI/LEVEL-2/OL_2_LFR___/{0}/{1}/{2}/{3}.zip/{3}.SEN3".format(
                        product_type_match.group('yyyy'),
                        product_type_match.group('mm'),
                        product_type_match.group('dd'),
                        product_type_match.group('id')
                    )
                    copy_tree(source, "{0}/{1}.SEN3".format(input_dir, product_type_match.group('id')), "xfdumanifest.xml")
                else:
                    raise("No appropriate download source found")
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

        else:
            raise Exception("No matching type found")

    elif provider == 'SOBLOO':
        # Use DirectData API (with API key, passed as the credentials password)

        sobloo_api_key = credentials_match.group('password')
        if product_type == 'OL_2_LFR___':
            url_regex = re.compile(r'.*\.SEN3(/(?P<dir>[^\?]*))?(/(?P<file>[^/\?]+))(\?.*)?')

            response = requests.post("https://sobloo.eu/api/v1-beta/direct-data/product-links",
                headers = {'Authorization': "Apikey {0}".format(sobloo_api_key)},
                json={'product': product_type_match.group('id'), 'regexp': ".*"}
            )
            result = json.loads(response.text)
            
            download_list = [ l['url'] for l in result['links'] ]
            for url in download_list:
                m = url_regex.match(url)
                if not m:
                    raise('Unrecognised URL pattern: {0}'.format(url))

                file_dir = "{0}.SEN3/{1}{2}".format(product_type_match.group('id'), '/' if m.group('dir') else '', m.group('dir') if m.group('dir') else '')
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

except Exception as e:
    print("ERROR during stage in of {0}: {1}".format(input_id, str(e)), file=sys.stderr)
    sys.exit(1)
