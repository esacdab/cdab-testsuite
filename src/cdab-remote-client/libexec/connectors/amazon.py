import boto3
from cdab_shared import *
import os
import pytz
import time
import sys





class AmazonConnector:

    def __init__(self, client):
        self.client = client
        self.compute_config = self.client.compute_config




    def initialize(self, compute_parameters):

        error = False
        for name in [ 
            'username',
            'password',
            'region_name',
            'key_name',
            'vm_name',
            'image_name',
            'flavor_name',
            'security_group',
            'private_key_file',
            'remote_user'
        ]:
            if name not in self.compute_config or self.compute_config[name] is None:
                error = True
                Logger.log(LogLevel.ERROR, "No value found for configuration key '{0}' ({1})".format(name, next((p['description'] for p in compute_parameters if 'name' in p and p['name'] == name and 'description' in p), 'no description available')))

        if error:
            exit_client(ERR_CONFIG, "Values missing for one or more configuration keys")

        if 'floating_ip' in self.compute_config and self.compute_config['floating_ip']:
            Logger.log(LogLevel.WARN, "Amazon AWS EC2 instances have external IP addresses assigned automatically, 'floating_ip' setting is ignored")

        os.environ["AWS_ACCESS_KEY_ID"] = self.compute_config['username']
        os.environ["AWS_SECRET_ACCESS_KEY"] = self.compute_config['password']
        os.environ["AWS_DEFAULT_REGION"] = self.compute_config['region_name']

        self.ec2_resource = boto3.resource('ec2')
        self.ec2_client = boto3.client('ec2')



    def delete_old_resources(self, max_retention_hours):

        now = datetime.datetime.now().astimezone(pytz.utc)   # not utc_now(), astimezone(pytz.utc), which is required for difference calculation, 
                                                             # still interprets it as local time
        # Find and delete old VMs
        response = self.ec2_client.describe_instances()

        unused_vms = []
        for reservation in response['Reservations']:
            for instance in reservation['Instances']:
                launch_time = instance['LaunchTime'].astimezone(pytz.utc)
                time_diff = now - launch_time
                if time_diff.days * 24 + time_diff.seconds // 3600 >= max_retention_hours:
                    unused_vms.append({'id': instance['InstanceId'], 'created_time': launch_time})

        if unused_vms:
            try:
                self.ec2_client.terminate_instances(
                    InstanceIds=[v['id'] for v in unused_vms]
                )
                for v in unused_vms:
                    Logger.log(LogLevel.INFO, "Virtual machine '{0}' deleted (created on {1}, more than {2} hours ago)".format(v['id'], v['created_time'].strftime('%Y-%m-%dT%H:%M:%S.%fZ'), max_retention_hours))

                Logger.log(LogLevel.INFO, "{0} virtual machine(s) deleted".format(len(unused_vms)))
            except Exception as e:
                Logger.log(LogLevel.WARN, "Error during deletion of virtual machine(s): {0}".format(str(e)))

        # Find and delete old volumes
        response = self.ec2_client.describe_volumes()

        unused_volumes = []
        for volume in response['Volumes']:
            created_time = volume['CreateTime'].astimezone(pytz.utc)
            time_diff = now - created_time
            if time_diff.days * 24 + time_diff.seconds // 3600 >= max_retention_hours:
                unused_volumes.append({'id': volume['VolumeId'], 'created_time': created_time})

        if unused_volumes:
            deleted = 0
            for v in unused_volumes:
                try:
                    self.ec2_client.delete_volume(
                        VolumeId=v['id']
                    )
                    Logger.log(LogLevel.INFO, "Volume '{0}' deleted (created on {1}, more than {2} hours ago)".format(v['id'], v['created_time'].strftime('%Y-%m-%dT%H:%M:%S.%fZ'), max_retention_hours))

                    deleted += 1

                except Exception as e:
                    Logger.log(LogLevel.WARN, "Error during deletion of volume: {0}".format(str(e)))
            
            Logger.log(LogLevel.INFO, "{0} volume(s) deleted".format(deleted))
                


    def prepare(self):
        pass
    

    
    def create_vm(self, run):

        Logger.log(LogLevel.INFO, "Creating virtual machine ...", run=run)


        try:
            run.create_start_time = datetime.datetime.utcnow()
            instances = self.ec2_resource.create_instances(
                ImageId=self.compute_config['image_name'],
                SecurityGroupIds=[self.compute_config['security_group']],
                BlockDeviceMappings=[
                    {
                        'DeviceName': "/dev/sda1",
                        'Ebs': {
                            'DeleteOnTermination': True,
                            'VolumeSize': 20,
                        }
                    }
                ],
                MinCount=1,
                MaxCount=1,
                InstanceType=run.flavor,
                KeyName=self.compute_config['key_name']
            )

            instance = instances[0]
            run.vm_id = instance.instance_id
            
            instance.wait_until_running()

            response = self.ec2_client.describe_instances(
                InstanceIds=[instance.instance_id]
            )

            instance = next((i for r in response['Reservations'] for i in r['Instances']), None)
            if instance and 'PublicIpAddress' in instance:
                run.public_ip = instance['PublicIpAddress']

            if run.public_ip:
                Logger.log(LogLevel.INFO, "IP address is {0}".format(run.public_ip), run=run)
            else:
                raise Exception("No IP address found")

        except Exception as e:
            exit_client(ERR_CREATE, "Error during creation of Amazon AWS EC2 VM instance: {0}".format(str(e)))

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
                response = self.ec2_client.terminate_instances(
                    InstanceIds=[run.vm_id]
                )
                
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



