from cdab_shared import *
import datetime
from enum import Enum
import io
import json
import os
from os import path
import re
import socket
import subprocess
import sys
import threading
import time
import xml.etree.ElementTree as ET
import yaml



class OpenStackConnector:

    def __init__(self, client):
        self.client = client
        self.compute_config = self.client.compute_config
        self.ip_addresses = []

    

    def initialize(self, compute_parameters):
        error = False
        for name in [ 
            'auth_url',
            'username',
            'password',
            'project_name',
            'user_domain_name',
            'interface',
            'identity_api_version',
            'vm_name',
            'key_name',
            'image_name',
            'flavor_name',
            'private_key_file',
            'remote_user',
        ]:
            if name not in self.compute_config or self.compute_config[name] is None:
                error = True
                Logger.log(LogLevel.ERROR, "No value found for configuration key '{0}' ({1})".format(name, next((p['description'] for p in compute_parameters if 'name' in p and p['name'] == name and 'description' in p), 'no description available')))

        if error:
            exit_client(ERR_CONFIG, "Values missing for one or more configuration keys")

        # OpenStack options that have to be used at every call
        self.cloud_base_options = [
            '--os-auth-url', self.compute_config['auth_url'],
            '--os-username', self.compute_config['username'],
            '--os-password', (self.compute_config['password'], 'xxxxxxxx'),
        ]
        if self.compute_config['project_id']:
            self.cloud_base_options.extend(['--os-project-id', self.compute_config['project_id']])
        self.cloud_base_options.extend([
            '--os-project-name', self.compute_config['project_name'],
            '--os-user-domain-name', self.compute_config['user_domain_name'],
        ])
        if self.compute_config['region_name']:
            self.cloud_base_options.extend(['--os-region-name', self.compute_config['region_name']])
        self.cloud_base_options.extend([
            '--os-interface', self.compute_config['interface'],
            '--os-identity-api-version', self.compute_config['identity_api_version'],
        ])
        if self.compute_config['volume_api_version']:
            self.cloud_base_options.extend([
                '--os-volume-api-version', self.compute_config['volume_api_version'],
            ])

        

    def delete_old_resources(self, max_retention_hours):
        
        now = datetime.datetime.utcnow()

        # Find and delete old VMs
        options = ['openstack', 'server', 'list', '-f', 'json']
        options.extend(self.cloud_base_options)
        response = execute_local_command(None, options, True)
        ids = [ r["ID"] for r in response if "ID" in r ]
        unused_vms = []
        for id in ids:
            try:
                options = ['openstack', 'server', 'show', '-f', 'json']
                options.extend(self.cloud_base_options)
                options.append(id)
                response = execute_local_command(None, options, True)
                if 'name' not in response or 'created' not in response:
                    continue
                name = response['name']
                if not name.startswith(self.compute_config['vm_name']):
                    continue
                created_time = datetime.datetime.strptime(response['created'], '%Y-%m-%dT%H:%M:%SZ')
                time_diff = now - created_time
                if time_diff.days * 24 + time_diff.seconds // 3600 >= max_retention_hours:
                    unused_vms.append({'id': id, 'name': name, 'created_time': created_time})
            except Exception as e:
                Logger.log(LogLevel.WARN, "Error while accessing information of virtual machine '{0}': {1}".format(id, str(e)))

        if unused_vms:
            options = ['openstack', 'server', 'delete']
            options.extend(self.cloud_base_options)
            options.extend(v['id'] for v in unused_vms)
            try:
                execute_local_command(None, options, False)
                for v in unused_vms:
                    Logger.log(LogLevel.INFO, "Virtual machine '{0}' (ID: '{1}') deleted (created on {2}, more than {3} hours ago)".format(v['name'], v['id'], v['created_time'].strftime('%Y-%m-%dT%H:%M:%S.%fZ'), max_retention_hours))
                Logger.log(LogLevel.INFO, "{0} virtual machine(s) deleted".format(len(unused_vms)))
            except Exception as e:
                Logger.log(LogLevel.WARN, "Error during deletion of virtual machine(s): {0}".format(str(e)))
        
        # Find and delete old volumes
        options = ['openstack', 'volume', 'list', '-f', 'json']
        options.extend(self.cloud_base_options)
        response = execute_local_command(None, options, True)
        ids = [ r["ID"] for r in response if "ID" in r ]
        unused_volumes = []
        for id in ids:
            try:
                options = ['openstack', 'volume', 'show', '-f', 'json']
                options.extend(self.cloud_base_options)
                options.append(id)
                response = execute_local_command(None, options, True)
                if 'name' not in response or 'created_at' not in response:
                    continue
                name = response['name']
                if not name.startswith(self.compute_config['vm_name']):
                    continue
                created_time = datetime.datetime.strptime(response['created_at'], '%Y-%m-%dT%H:%M:%S.%f')
                time_diff = now - created_time
                if time_diff.days * 24 + time_diff.seconds // 3600 >= max_retention_hours:
                    unused_volumes.append({'id': id, 'name': name, 'created_time': created_time})
            except Exception as e:
                Logger.log(LogLevel.WARN, "Error while accessing information of virtual machine '{0}': {1}".format(id, str(e)))

        if unused_volumes:
            options = ['openstack', 'volume', 'delete']
            options.extend(self.cloud_base_options)
            options.extend([v['id'] for v in unused_volumes])
            try:
                execute_local_command(None, options, False)
                for v in unused_volumes:
                    Logger.log(LogLevel.INFO, "Volume '{0}' (ID: '{1}') deleted (created on {2}, more than {3} hours ago)".format(v['name'], v['id'], v['created_time'].strftime('%Y-%m-%dT%H:%M:%S.%fZ'), max_retention_hours))
                Logger.log(LogLevel.INFO, "{0} volume(s) deleted".format(len(unused_volumes)))
            except Exception as e:
                Logger.log(LogLevel.WARN, "Error during deletion of volume(s): {0}".format(str(e)))



    
    def prepare(self):
        if self.compute_config['floating_ip']:
            self.find_floating_ips(self.client.total_vm_count)


  
    
    def copy_additional_files(self, run):
        pass




    def add_supplier(self, suppliers):
        pass



    def create_vm(self, run):

        Logger.log(LogLevel.INFO, "Creating virtual machine ...", run=run)

        options = ['openstack', 'server', 'create', '--wait', '-f', 'json']
        options.extend(self.cloud_base_options)
        options.extend([
            '--image', self.compute_config['image_name'],
            '--flavor', run.flavor
        ])
        if self.compute_config['network_name']:
            for network_name in self.compute_config['network_name']:
                options.extend(['--network', network_name])
        if self.compute_config['security_group']:
            options.extend(['--security-group', self.compute_config['security_group']])
        options.extend([
            '--key-name', self.compute_config['key_name'],
            "{0}{1}".format(self.compute_config['vm_name'], run.suffix)
        ])

        run.create_start_time = datetime.datetime.utcnow()
        response = execute_local_command(run, options, True)

        Logger.log(LogLevel.DEBUG, response, run=run)

        if 'id' in response and response['id']:
            run.vm_id = response['id']
            Logger.log(LogLevel.INFO, "Virtual machine '{0}' created".format(run.vm_id), run=run)

        if run.vm_id is None:
            raise Exception("No virtual machine ID found")

        if self.compute_config['floating_ip']:
            self.assign_floating_ip(run)
        elif 'addresses' in response:
            ip_match = re.match(r".*(, |=)((\d{1,3}\.\d{1,3})\.\d{1,3}\.\d{1,3}).*", response['addresses'])
            if ip_match:
                run.public_ip = ip_match[2]
                Logger.log(LogLevel.INFO, "IP address {0} assigned automatically at creation".format(run.public_ip), run=run)

            if run.public_ip is None:
                raise Exception("No IP address found: {0}".format(response['addresses']))
                return False


        # Wait for actual availability (SSH):
        connect_start_time = datetime.datetime.utcnow()
        available = await_vm_availability(self.compute_config, self.client.connect_retries, self.client.connect_interval, run)

        if available:
            run.ssh_ready_time = datetime.datetime.utcnow()
            Logger.log(LogLevel.INFO, "Virtual machine available", run=run)
        else:
            Logger.log(LogLevel.ERROR, "Virtual machine not available after {0} seconds".format(int((datetime.datetime.utcnow() - connect_start_time).total_seconds())), run=run)

        if available and (self.compute_config['use_volume'] or self.compute_config['use_tmp_volume']):
            if self.compute_config['use_volume']:
                Logger.log(LogLevel.INFO, "Creating main volume for virtual machine ...", run=run)
                options = ['openstack', 'volume', 'create', '-f', 'json']
                options.extend(self.cloud_base_options)
                options.extend([
                    "--size", "100",
                    "{0}{1}-volume".format(self.compute_config['vm_name'], run.suffix)
                ])
                response = execute_local_command(run, options, True)

                if 'id' in response and response['id']:
                    run.volume_id = response['id']
                    Logger.log(LogLevel.INFO, "Volume '{0}' created".format(run.volume_id), run=run)

                if run.volume_id is None:
                    raise Exception("No volume found for volume")

                Logger.log(LogLevel.INFO, "Attaching volume to virtual machine ...", run=run)
                options = ['openstack', 'server', 'add', 'volume']
                options.extend(self.cloud_base_options)
                options.extend([
                    run.vm_id,
                    run.volume_id,
                ])
                execute_local_command(run, options, False)

            if self.compute_config['use_tmp_volume']:
                Logger.log(LogLevel.INFO, "Creating /tmp volume for virtual machine ...", run=run)
                options = ['openstack', 'volume', 'create', '-f', 'json']
                options.extend(self.cloud_base_options)
                options.extend([
                    "--size", "50",
                    "{0}{1}-tmp-volume".format(self.compute_config['vm_name'], run.suffix)
                ])
                response = execute_local_command(run, options, True)

                if 'id' in response and response['id']:
                    run.tmp_volume_id = response['id']
                    Logger.log(LogLevel.INFO, "Volume '{0}' created".format(run.tmp_volume_id), run=run)

                if run.tmp_volume_id is None:
                    raise Exception("No ID found for /tmp volume")

                Logger.log(LogLevel.INFO, "Attaching volume to virtual machine ...", run=run)
                options = ['openstack', 'server', 'add', 'volume']
                options.extend(self.cloud_base_options)
                options.extend([
                    run.vm_id,
                    run.tmp_volume_id,
                ])
                execute_local_command(run, options, False)

            time.sleep(15)

            options = ['openstack', 'volume', 'list', '-f', 'json']
            options.extend(self.cloud_base_options)
            response = execute_local_command(run, options, True)

            if self.compute_config['use_volume']:
                volume_node = next((r for r in response if r['ID'] == run.volume_id), None)

                if volume_node and 'Attached to' in volume_node and volume_node['Attached to']:
                    if isinstance(volume_node['Attached to'], list):
                        for a in volume_node['Attached to']:
                            if 'server_id' in a and 'device' in a and a['server_id'] == run.vm_id:
                                run.volume_device = a['device']
                                break
                    else:
                        device_match = re.match(r".* on (/dev/[^ ]+).*", volume_node['Attached to'])
                        if device_match:
                            run.volume_device = device_match[1]

                if run.volume_device is None:
                    raise Exception("No volume device found (main volume)")

                run.volume_attached = True

            if self.compute_config['use_tmp_volume']:
                volume_node = next((r for r in response if r['ID'] == run.tmp_volume_id), None)

                if volume_node and 'Attached to' in volume_node and volume_node['Attached to']:
                    if isinstance(volume_node['Attached to'], list):
                        for a in volume_node['Attached to']:
                            if 'server_id' in a and 'device' in a and a['server_id'] == run.vm_id:
                                run.tmp_volume_device = a['device']
                                break
                    else:
                        device_match = re.match(r".* on (/dev/[^ ]+).*", volume_node['Attached to'])
                        if device_match:
                            run.tmp_volume_device = device_match[1]

                if run.tmp_volume_device is None:
                    raise Exception("No volume device found (/tmp volume)")

                run.tmp_volume_attached = True


            setup_disk_file = "setup-disk{0}.sh".format(run.suffix)

            with open(setup_disk_file, 'w') as file:
                file.write("parted {0} mklabel gpt\n".format(run.volume_device))
                file.write("parted {0} unit GB \n".format(run.volume_device))
                file.write("parted {0} mkpart primary 0% 100%\n".format(run.volume_device))
                file.write("mkfs.ext4 {0}1\n".format(run.volume_device))
                file.write("mkdir /mnt/cdab-volume\n")
                file.write("mkdir /mnt/cdab-volume/test\n")
                file.write("chown {0} /mnt/cdab-volume/test\n".format(self.compute_config['remote_user']))
                file.write("echo '{0}1 /mnt/cdab-volume ext4 defaults 0 2' >> /etc/fstab\n".format(run.volume_device))

                if self.compute_config['use_tmp_volume']:
                    file.write("parted {0} mklabel gpt\n".format(run.tmp_volume_device))
                    file.write("parted {0} unit GB \n".format(run.tmp_volume_device))
                    file.write("parted {0} mkpart primary 0% 100%\n".format(run.tmp_volume_device))
                    file.write("mkfs.ext4 {0}1\n".format(run.tmp_volume_device))
                    file.write("mkdir /mnt/cdab-volume/tmp\n")
                    file.write("echo '{0}1 /tmp ext4 defaults 0 0' >> /etc/fstab\n".format(run.tmp_volume_device))

                file.write("mount -a\n")

                if self.compute_config['use_tmp_volume']:
                    file.write("chmod 1777 /tmp\n")

                file.close()

            copy_file(self.compute_config, run, setup_disk_file, "setup-disk.sh")
            execute_remote_command(self.compute_config, run, "sudo sh setup-disk.sh")

        return available




    def delete_vm(self, run):
        if run.vm_id is None:
            return True

        max_retries = 3
        if self.compute_config['use_volume'] and run.volume_id and run.volume_attached:
            Logger.log(LogLevel.INFO, "Detaching main volume from virtual machine ...", run=run)
            options = ['openstack', 'server', 'remove', 'volume']
            options.extend(self.cloud_base_options)
            options.extend([
                run.vm_id,
                run.volume_id,
            ])
            execute_local_command(run, options, False)
            time.sleep(10)

            Logger.log(LogLevel.INFO, "Deleting volume {0} ...".format(run.volume_id), run=run)
            options = ['openstack', 'volume', 'delete']
            options.extend(self.cloud_base_options)
            options.extend([
                run.volume_id,
            ])
            
            retry = 0
            deleted = False
            while retry < max_retries and not deleted:
                try:
                    execute_local_command(run, options, False)
                    run.delete_end_time = datetime.datetime.utcnow()
                    Logger.log(LogLevel.INFO, "Volume deleted", run=run)
                    deleted = True
                except Exception as e:
                    retry += 1

                    if retry == max_retries:
                        self.client.incomplete_deletion = True
                        print("********************************************************************", file=run.stderr)
                        Logger.log(LogLevel.ERROR, "Failed to delete volume '{0}'".format(run.volume_id), run=run)
                        Logger.log(LogLevel.ERROR, "Message: {0}".format(str(e)), run=run)
                        Logger.log(LogLevel.ERROR, "Delete manually", run=run)
                        print("********************************************************************", file=run.stderr)
                    else:
                        Logger.log(LogLevel.WARN, "Deletion failed, retrying after 30 seconds", run=run)
                        time.sleep(30)


        if self.compute_config['use_tmp_volume'] and run.tmp_volume_id and run.tmp_volume_attached:
            Logger.log(LogLevel.INFO, "Detaching /tmp volume from virtual machine ...", run=run)
            options = ['openstack', 'server', 'remove', 'volume']
            options.extend(self.cloud_base_options)
            options.extend([
                run.vm_id,
                run.tmp_volume_id,
            ])
            execute_local_command(run, options, False)
            time.sleep(10)

            Logger.log(LogLevel.INFO, "Deleting volume {0} ...".format(run.volume_id), run=run)
            options = ['openstack', 'volume', 'delete']
            options.extend(self.cloud_base_options)
            options.extend([
                run.tmp_volume_id,
            ])
            
            retry = 0
            deleted = False
            while retry < max_retries and not deleted:
                try:
                    execute_local_command(run, options, False)
                    run.delete_end_time = datetime.datetime.utcnow()
                    Logger.log(LogLevel.INFO, "Volume deleted", run=run)
                    deleted = True
                except Exception as e:
                    retry += 1

                    if retry == max_retries:
                        self.client.incomplete_deletion = True
                        print("********************************************************************", file=run.stderr)
                        Logger.log(LogLevel.ERROR, "Failed to delete volume '{0}'".format(run.tmp_volume_id), run=run)
                        Logger.log(LogLevel.ERROR, "Message: {0}".format(str(e)), run=run)
                        Logger.log(LogLevel.ERROR, "Delete manually", run=run)
                        print("********************************************************************", file=run.stderr)
                    else:
                        Logger.log(LogLevel.WARN, "Deletion failed, retrying after 30 seconds", run=run)
                        time.sleep(30)


        Logger.log(LogLevel.INFO, "Deleting virtual machine '{0}' ...".format(run.vm_id), run=run)

        options = ['openstack', 'server', 'delete']
        options.extend(self.cloud_base_options)
        options.extend([
            run.vm_id
        ])

        retry = 0
        deleted = False
        while retry < max_retries and not deleted:
            try:
                execute_local_command(run, options, False)
                run.delete_end_time = datetime.datetime.utcnow()
                Logger.log(LogLevel.INFO, "Virtual machine deleted", run=run)
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


        return not self.client.incomplete_deletion



    def assign_floating_ip(self, run):

        Logger.log(LogLevel.INFO, "Assigning floating IP address ...", run=run)

        if run.index >= len(self.ip_addresses):
            Logger.log(LogLevel.ERROR, "No IP address available", run=run)

        ip_address = self.ip_addresses[run.index]

        options = ['openstack', 'server', 'add', 'floating ip']
        options.extend(self.cloud_base_options)
        options.extend([run.vm_id, ip_address])

        response = execute_local_command(run, options, False)
        run.public_ip = ip_address

        Logger.log(LogLevel.INFO, "IP address {0} assigned explicitly".format(run.public_ip), run=run)



    def find_floating_ips(self, number_needed):
        Logger.log(LogLevel.INFO, "Obtaining list of available floating IP addresses ...")

        options = ['openstack', 'floating ip', 'list', '-f', 'json']
        options.extend(self.cloud_base_options)
        if self.compute_config['floating_ip_network']:
            options.extend(['--network', self.compute_config['floating_ip_network']])

        response = execute_local_command(None, options, True)

        self.ip_addresses = [r['Floating IP Address'] for r in response if r['Fixed IP Address'] is None]

        if self.ip_addresses == []:
            exit_client(ERR_CREATE, "No floating IP address available")

        Logger.log(LogLevel.INFO, "Available floating IP addresses: {0}".format(", ".join(self.ip_addresses)))

        if len(self.ip_addresses) < number_needed:
            exit_client(ERR_CREATE, "Only {0} floating IP address available".format(len(self.ip_addresses)))






