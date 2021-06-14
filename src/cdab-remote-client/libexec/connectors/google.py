from cdab_shared import *
import googleapiclient.discovery
import os
import pytz
import re
import time
import sys



class GoogleConnector:

    def __init__(self, client):
        self.client = client
        self.compute_config = self.client.compute_config
        self.service_provider_config = self.client.service_provider_config



    def initialize(self, compute_parameters):
        error = False

        if 'account_file' not in self.compute_config or not self.compute_config['account_file']:
            if 'account_file' in self.service_provider_config and self.service_provider_config['account_file']:
                self.compute_config['account_file'] = self.service_provider_config['account_file']
            elif 'auth_file' in self.compute_config and self.compute_config['auth_file']:
                self.compute_config['account_file'] = self.compute_config['auth_file']

        if 'project_id' not in self.compute_config or not self.compute_config['project_id']:
            if 'project_id' in self.service_provider_config and self.service_provider_config['project_id']:
                self.compute_config['project_id'] = self.service_provider_config['project_id']
            elif 'project_name' in self.compute_config and self.compute_config['project_name']:
                self.compute_config['project_id'] = self.compute_config['project_name']

        for name in [ 
            'account_file',
            'project_id',
            'region_name',
            'vm_name',
            'image_name',
            'flavor_name',
            'private_key_file',
            'remote_user'
        ]:
            if name not in self.compute_config or self.compute_config[name] is None:
                error = True
                Logger.log(LogLevel.ERROR, "No value found for configuration key '{0}' ({1})".format(name, next((p['description'] for p in compute_parameters if 'name' in p and p['name'] == name and 'description' in p), 'no description available')))

        if error:
            exit_client(ERR_CONFIG, "Values missing for one or more configuration keys")

        if 'floating_ip' in self.compute_config and self.compute_config['floating_ip']:
            Logger.log(LogLevel.WARN, "Google Cloud Platform instances have external IP addresses assigned automatically, 'floating_ip' setting is ignored")

        os.environ["GOOGLE_APPLICATION_CREDENTIALS"] = self.compute_config['account_file']

        self.compute = googleapiclient.discovery.build('compute', 'v1')



    def delete_old_resources(self, max_retention_hours):
        now = datetime.datetime.now().astimezone(pytz.utc)   # not utc_now(), astimezone(pytz.utc), which is required for difference calculation, 
                                                             # still interprets it as local time

        unused_vms = []
        response = self.compute.instances().list(
            project=self.compute_config['project_id'],
            zone=self.compute_config['region_name'],
        ).execute()

        if 'items' in response:
            for item in response['items']:
                if not item['name'].startswith(self.compute_config['vm_name']) or item['name'].lower().startswith('k-'):
                    continue
                ct = re.sub('\.\d+([\+-]\d\d):?(\d\d)', '\g<1>\g<2>', item['creationTimestamp'])   # Python 3.6 issue, strip milliseconds and ':' in time zone offset
                created_time = datetime.datetime.strptime(ct, '%Y-%m-%dT%H:%M:%S%z').astimezone(pytz.utc)
                time_diff = now - created_time
                if time_diff.days * 24 + time_diff.seconds // 3600 >= max_retention_hours:
                    unused_vms.append({'id': item['id'], 'name': item['name'], 'created_time': created_time})

            if unused_vms:
                deleted = 0
                for v in unused_vms:
                    try:
                        operation = self.compute.instances().delete(
                            project=self.compute_config['project_id'],
                            zone=self.compute_config['region_name'],
                            instance=v['id']
                        ).execute()
                        self.wait_for_operation(operation['name'], self.compute)
                        Logger.log(LogLevel.INFO, "Virtual machine '{0}' (ID: '{1}') deleted (created on {2}, more than {3} hours ago)".format(v['name'], v['id'], v['created_time'].strftime('%Y-%m-%dT%H:%M:%S.%fZ'), max_retention_hours))

                        deleted += 1

                    except Exception as e:
                        Logger.log(LogLevel.WARN, "Error during deletion of virtual machine: {0}".format(str(e)))
                
                Logger.log(LogLevel.INFO, "{0} virtual machine(s) deleted".format(deleted))
            



    def prepare(self):
        pass




    def copy_additional_files(self, run):
        dirname = os.path.dirname(self.compute_config['account_file'])
        execute_remote_command(self.compute_config, run, "sudo mkdir -p {0}".format(dirname))
        execute_remote_command(self.compute_config, run, "sudo chown {0} {1}".format(self.compute_config['remote_user'], dirname))
        copy_file(self.compute_config, run, self.compute_config['account_file'], self.compute_config['account_file'])

    

    def add_supplier(self, suppliers):
        suppliers['GOOGLE'] = {
            'Type': "Terradue.Data.Stars.Suppliers.DataHubSourceSupplier",
            'ServiceUrl': "https://storage.googleapis.com",
            'projectId': self.compute_config['project_id'],
            'AccountFile': self.compute_config['account_file'],
        }


        
    def create_vm(self, run):

        run.compute = googleapiclient.discovery.build('compute', 'v1')

        Logger.log(LogLevel.INFO, "Creating virtual machine ...", run=run)

        # If flavour is shorthand, make it fully qualified
        if '/' in run.flavor:
            flavor = run.flavor
        else:
            flavor = "projects/{0}/zones/{1}/machineTypes/{2}".format(self.compute_config['project_id'], self.compute_config['region_name'], run.flavor)

        config = {
            'name': "{0}{1}".format(self.compute_config['vm_name'], run.suffix),
            'machineType': flavor,
            'disks': [
                {
                    'boot': True,
                    'autoDelete': True,
                    'initializeParams': {
                        'sourceImage': self.compute_config['image_name'],
                        'diskSizeGb': '20'
                    }
                }
            ],
            'networkInterfaces': [
                {
                    'network': 'global/networks/default',
                    'accessConfigs': [
                        {'type': 'ONE_TO_ONE_NAT', 'name': 'External NAT'}
                    ]
                }
            ],
            # Allow the instance to access cloud storage and logging.
            'serviceAccounts': [{
                'email': 'default',
                'scopes': [
                    'https://www.googleapis.com/auth/devstorage.read_write',
                    'https://www.googleapis.com/auth/logging.write'
                ]
            }]
        }

        try:
            run.create_start_time = datetime.datetime.utcnow()
            operation = run.compute.instances().insert(
                project=self.compute_config['project_id'],
                zone=self.compute_config['region_name'],
                body=config
            ).execute()
    
            if 'targetId' in operation:
                run.vm_id = operation['targetId']
                Logger.log(LogLevel.INFO, "Virtual machine '{0}{1}' created (target ID = {2})".format(self.compute_config['vm_name'], run.suffix, run.vm_id), run=run)
            else:
                raise Exception("No targetId in response")

            self.wait_for_operation(operation['name'], run.compute)

            response = run.compute.instances().get(
                instance="{0}{1}".format(self.compute_config['vm_name'], run.suffix),
                project=self.compute_config['project_id'],
                zone=self.compute_config['region_name'],
            ).execute()

            if 'networkInterfaces' in response:
                for ni in response['networkInterfaces']:
                    if 'accessConfigs' in ni:
                        for ac in ni['accessConfigs']:
                            if 'name' in ac and ac['name'] == 'External NAT':
                                run.public_ip = ac['natIP']
                                break
                    if run.public_ip:
                        break

            if run.public_ip:
                Logger.log(LogLevel.INFO, "IP address is {0}".format(run.public_ip), run=run)
            else:
                raise Exception("No IP address found")

        except Exception as e:
            exit_client(ERR_CREATE, "Error during creation of Google VM instance: {0}".format(str(e)))

        # Wait for actual availability (SSH):
        connect_start_time = datetime.datetime.utcnow()
        available = await_vm_availability(self.compute_config, self.client.connect_retries, self.client.connect_interval, run)

        if available:
            run.ssh_ready_time = datetime.datetime.utcnow()
            Logger.log(LogLevel.INFO, "Virtual machine available", run=run)
        else:
            Logger.log(LogLevel.ERROR, "Virtual machine not available after {0} seconds".format(int((datetime.datetime.utcnow() - connect_start_time).total_seconds())), run=run)

        return available





    def delete_vm(self, run):
        if run.vm_id is None:
            return True

        Logger.log(LogLevel.INFO, "Deleting virtual machine '{0}{1}' ...".format(self.compute_config['vm_name'], run.suffix), run=run)

        max_retries = 3
        retry = 0
        deleted = False
        while retry < max_retries and not deleted:
            try:
                operation = run.compute.instances().delete(
                    project=self.compute_config['project_id'],
                    zone=self.compute_config['region_name'],
                    instance=run.vm_id
                ).execute()
                self.wait_for_operation(operation['name'], run.compute)

                Logger.log(LogLevel.INFO, "Virtual machine deleted", run=run)
                run.delete_end_time = datetime.datetime.utcnow()
                deleted = True

            except Exception as e:
                retry += 1

                if retry == max_retries:
                    self.client.incomplete_deletion = True
                    print("********************************************************************", file=run.stderr)
                    Logger.log(LogLevel.ERROR, "Failed to delete virtual machine '{0}'".format(run.vm_id), run=run)
                    Logger.log(LogLevel.ERROR, "Message: {0}".format(str(e)), run=run)
                    Logger.log(LogLevel.ERROR, "Delete manually", run=run)
                    print("********************************************************************", file=run.stderr)
                else:
                    Logger.log(LogLevel.WARN, "Deletion failed, retrying after 30 seconds", run=run)
                    time.sleep(30)




    def wait_for_operation(self, operation, compute):
        while True:
            response = compute.zoneOperations().get(
                project=self.compute_config['project_id'],
                zone=self.compute_config['region_name'],
                operation=operation).execute()

            if response['status'] == 'DONE':
                if 'error' in response:
                    raise Exception(response['error'])
                return response

            time.sleep(1)


