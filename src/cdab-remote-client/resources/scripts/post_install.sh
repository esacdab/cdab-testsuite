#!/bin/bash

SUCCESS=0

# Install OpenStack client, Google Cloud Platform Python API and Amazon AWS EC2 Python API
/opt/rh/rh-python36/root/usr/bin/pip install python-openstackclient==5.1.0
/opt/rh/rh-python36/root/usr/bin/pip install google-api-python-client boto3

# Add symlink to cdab-remote-client
ln -s /var/opt/cdab-remote-client/bin/cdab-remote-client /usr/bin/cdab-remote-client

/opt/rh/rh-python36/root/usr/bin/pip install --upgrade pip

exit ${SUCCESS}
