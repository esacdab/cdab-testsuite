import sys
import os
import re
import requests

input_dir = sys.argv[1]
product_id = sys.argv[2]
sobloo_api_key = sys.argv[3]

response = requests.post("https://sobloo.eu/api/v1-beta/direct-data/product-links",
    headers = {'Authorization': "Apikey {0}".format(sobloo_api_key)},
    json={'product': product_id, 'regexp': ".*"}
)
result = response.json()

download_list = [ l['url'] for l in result['links'] ]

url_regex = re.compile(r'.*\.SAFE(/(?P<dir>[^\?]*))?(/(?P<file>[^/\?]+))(\?.*)?')

for url in download_list:
    m = url_regex.match(url)
    if not m:
        raise('Unrecognised URL pattern: {0}'.format(url))

    file_dir = "{0}.SAFE/{1}{2}".format(product_id, '/' if m.group('dir') else '', m.group('dir') if m.group('dir') else '')
    file_name = m.group('file')

    complete_dir = "{0}/{1}".format(input_dir, file_dir)
    if not os.path.isdir(complete_dir):
        os.makedirs(complete_dir)

    location = "{0}/{1}".format(complete_dir, file_name)

    print(location)

    r = requests.get(url, stream=True)
    with open(location, 'wb') as f:
        for chunk in r.iter_content(chunk_size=8192):
            f.write(chunk)
    r.close()

