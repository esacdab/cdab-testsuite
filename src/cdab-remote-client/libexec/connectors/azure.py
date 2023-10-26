from cdab_shared import *
from azure.identity import DefaultAzureCredential
from azure.mgmt.compute import ComputeManagementClient
from azure.mgmt.network import NetworkManagementClient
from azure.mgmt.resource import ResourceManagementClient
import os
import datetime
import pytz
import re
import time
import sys



class AzureConnector:

    def __init__(self, client):
        self.client = client
        self.compute_config = self.client.compute_config
        self.service_provider_config = self.client.service_provider_config
        self.available_ip_configurations = []



    def initialize(self, compute_parameters):
        error = False

        for name in [ 
            'subscription_id',
            'tenant_id',
            'client_id',
            'client_secret',
            'resource_group_name',
            'region_name',
            'vm_name',
            'image',
            'private_key_file',
            'public_key_file',
            'remote_user'
        ]:
            if name not in self.compute_config or self.compute_config[name] is None:
                error = True
                Logger.log(LogLevel.ERROR, "No value found for configuration key '{0}' ({1})".format(name, next((p['description'] for p in compute_parameters if 'name' in p and p['name'] == name and 'description' in p), 'no description available')))

        if error:
            exit_client(ERR_CONFIG, "Values missing for one or more configuration keys")

        if 'floating_ip' in self.compute_config and self.compute_config['floating_ip']:
            Logger.log(LogLevel.WARN, "Azure instances require manual selection of IP addresses, 'floating_ip' setting is ignored")

        os.environ['AZURE_SUBSCRIPTION_ID'] = self.compute_config['subscription_id']
        os.environ['AZURE_TENANT_ID'] = self.compute_config['tenant_id']
        os.environ['AZURE_CLIENT_ID'] = self.compute_config['client_id']
        os.environ['AZURE_CLIENT_SECRET'] = self.compute_config['client_secret']

        self.credential = DefaultAzureCredential()
        self.resource_client = ResourceManagementClient(self.credential, self.compute_config['subscription_id'])
        self.network_client = NetworkManagementClient(self.credential, self.compute_config['subscription_id'])
        self.compute_client = ComputeManagementClient(self.credential, self.compute_config['subscription_id'])
        



    def delete_old_resources(self, max_retention_hours):
        now = datetime.datetime.now(datetime.timezone.utc)

        unused_vms = []
        vm_list = self.compute_client.virtual_machines.list(self.compute_config['resource_group_name'])

        for vm in vm_list:
            if not vm.name.startswith(self.compute_config['vm_name']) or vm.name.lower().startswith('k-'):
                continue
            created_time = vm.time_created
            time_diff = now - created_time
            if time_diff.days * 24 + time_diff.seconds // 3600 >= max_retention_hours:
                unused_vms.append({'id': vm.id, 'name': vm.name, 'created_time': created_time})

        if unused_vms:
            deleted = 0
            for v in unused_vms:
                try:
                    poller = self.compute_client.virtual_machines.begin_delete(
                        self.compute_config['resource_group_name'],
                        v['name']
                    )
                    vm_result = poller.result()

                    Logger.log(LogLevel.INFO, "Virtual machine '{0}' (ID: '{1}') deleted (created on {2}, more than {3} hours ago)".format(v['name'], v['id'], v['created_time'].strftime('%Y-%m-%dT%H:%M:%S.%fZ'), max_retention_hours))

                    deleted += 1

                except Exception as e:
                    Logger.log(LogLevel.WARN, "Error during deletion of virtual machine: {0}".format(str(e)))
            
            Logger.log(LogLevel.INFO, "{0} virtual machine(s) deleted".format(deleted))
            



    def prepare(self):
        self.find_floating_ips(self.client.total_vm_count)


    def copy_additional_files(self, run):
        pass
    

    def add_supplier(self, suppliers):
        pass


        
    def create_vm(self, run):

        Logger.log(LogLevel.INFO, "Creating virtual machine ...", run=run)

        if run.index >= len(self.available_ip_configurations):
            Logger.log(LogLevel.ERROR, "No IP address available", run=run)

        ip_configuration = self.available_ip_configurations[run.index]

        # If flavour is shorthand, make it fully qualified
        vm_config = {
            "name": self.compute_config['vm_name'],
            "type": "Microsoft.Compute/virtualMachines",
            "location": self.compute_config['region_name'],
            "zones": [
                "1"
            ],
            "hardware_profile": {
                "vm_size": run.flavor
            },
            "storage_profile": {
                "image_reference": self.compute_config['image']
            },
            "os_profile": {
                "computer_name": self.compute_config['vm_name'],
                "admin_username": self.compute_config['remote_user'],
                "linux_configuration": {
                    "disable_password_authentication": True,
                    "ssh": {
                        "public_keys": [
                            {
                                "path": "/home/{0}/.ssh/authorized_keys".format(self.compute_config['remote_user']),
                                "key_data": open(self.compute_config['public_key_file'], 'r').read()
                            }
                        ]
                    },
                },
            },
            "network_profile": {
                "network_interfaces": [
                    {
                        # "id": "/subscriptions/{0}/resourceGroups/{1}/providers/Microsoft.Network/networkInterfaces/{2}".format(
                        #     self.compute_config['subscription_id'],
                        #     self.compute_config['resource_group_name'],
                        #     interface_id,
                        # ),
                        "id": ip_configuration['id'],
                    }
                ]
            }
        }

        try:
            run.create_start_time = datetime.datetime.utcnow()
            poller = self.compute_client.virtual_machines.begin_create_or_update(
                self.compute_config['resource_group_name'],
                "{0}{1}".format(self.compute_config['vm_name'], run.suffix),
                vm_config   
            )

            vm_result = poller.result()
            run.vm_id = vm_result.id

            try:
                full_disk_id = vm_result.storage_profile.os_disk.managed_disk.id
                run.volume_id = full_disk_id.split('/')[-1]
            except:
                Logger.log(LogLevel.WARN, "Disk ID not retrieved (disk has to be deleted manually)", run=run)

            Logger.log(LogLevel.INFO, "Virtual machine '{0}{1}' created (ID = {2})".format(self.compute_config['vm_name'], run.suffix, run.vm_id), run=run)
    
            run.public_ip = ip_configuration['ip_address']

        except Exception as e:
            exit_client(ERR_CREATE, "Error during creation of Azure VM instance: {0}".format(str(e)))

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
                poller = self.compute_client.virtual_machines.begin_delete(
                    self.compute_config['resource_group_name'],
                    "{0}{1}".format(self.compute_config['vm_name'], run.suffix)
                )
                vm_result = poller.result()

                Logger.log(LogLevel.INFO, "Virtual machine deleted", run=run)

                Logger.log(LogLevel.INFO, "Deleting attached disk '{0}' ...".format(run.volume_id), run=run)
                poller = self.compute_client.disks.begin_delete(self.compute_config['resource_group_name'], run.volume_id)
                disk_result = poller.result()

                Logger.log(LogLevel.INFO, "Disk deleted", run=run)

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




    def find_floating_ips(self, number_needed):
        Logger.log(LogLevel.INFO, "Obtaining list of available public IP addresses ...")

        self.available_ip_configurations = []

        vm_list = self.compute_client.virtual_machines.list(self.compute_config['resource_group_name'])
        used_network_interfaces = [ni.id for vm in vm_list for ni in vm.network_profile.network_interfaces]

        rg = self.resource_client.resource_groups.get(self.compute_config['resource_group_name'])
        ip_list = self.network_client.public_ip_addresses.list(rg.name)
        for ip in ip_list:
            used = False
            ip_id = ip.ip_configuration.id.replace("/ipConfigurations/ipconfig1", "")
            for uni in used_network_interfaces:
                if uni == ip_id:
                    used = True
                    break
            if not used:
                self.available_ip_configurations.append({'id': ip_id, 'ip_address': ip.ip_address})

        if self.available_ip_configurations == []:
            exit_client(ERR_CREATE, "No public IP address available")

        Logger.log(LogLevel.INFO, "Available public IP addresses: {0}".format(", ".join([c['ip_address'] for c in self.available_ip_configurations])))

        if len(self.available_ip_configurations) < number_needed:
            exit_client(ERR_CREATE, "Only {0} public IP address available".format(len(self.ip_addresses)))

