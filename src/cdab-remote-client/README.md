# cdab-remote-client

The **cdab-remote-client** is a command line utility that performs a local CDAB test scenario on a test site where it uses a remote virtual machine that is created only for that purpose and deleted immediately afterwards. The tool measures the following metrics:

* **Error rate**: The percentage of failed tests.
* **Total duration**: The total duration (in milliseconds) of all fully successful test runs (from the request to create the virtual machine to its successful deletion).
* **Cost per hour**: The cost of the virtual machine per month (from the YAML configuration file).
* **Cost per month**: The cost of the virtual machine per month (from the YAML configuration file).
* **Average provisioning latency**: The time (in milliseconds) that passes between the request for the creation of the virtual machine and its readiness for remote access via SSH.
* **Average concurrency**: In case of more than one virtual machine (as specified in `-vm` option): the quotinent of the sum of the provisioning times for those virtual machines and the total time during which the provisioning takes place (the result is a value greater than or equal to 1 and less than or equal to the value of `-vm`); otherwise the value is 1.
* **Peak concurrency**: The maxium number of provisionings running in parallel during the provisioning period. The value can be at most the value of the `-vm` option.
* Cost (NOTE: not yet available).


## Getting started

Note: The Openstack terminology is inconsistent and might be confusing; **servers** (as in the command-line client) and **instances** (as on the dashboard) refer to the same thing, **virtual machines**, as they are called below.

### Prerequisites

* CentOS 7
* Python 3
* OpenStack client (a Python module that provides a command-line tool for OpenStack access)
* The Amazon AWS SDK for Python
* The Google API Client Library for Python
* Root or proper _sudo_ permissions

### Installation

* Add the terradue-stable-el7 repository configuration on YUM,
* Install the tool with:

```
$ sudo yum install cdab-remote-client -y
```


## Usage

The main parameters are shown on the help page.

Type

```
cdab-remote-client -h
```

And the output is the following:

```
cdab-remote-client version 1.47 (c) 2020 Terradue Srl.

USAGE: cdab-remote-client [OPTIONS] <test-scenario>

OPTIONS
    -h                        Display this help and exit
    -v                        Display more information during processing
    -ml                       Allow mixed log output (relevant for multiple parallel runs)
    -conf=<file>              YAML file containing the remote configuration
                              Default value: /opt/cdab-remote-client/etc/config.yaml
    -vm=<number>              Number of virtual machines to be run in parallel (min: 1)
                              Default value: 1
    -lf=<number>              Load factor (min: 1)
                              Default value: 1
    -sp=<name>                Service provider for test execution (as defined in configuration file)
    -ts=<name>                Target site for querying (as defined in configuration file)
    -te=<url>                 Endpoint URL for remote target calls (overrides settings from target site set with -ts)
    -tc=<username:password>   Credentials for target (overrides settings from target site set with -ts)
    -psw=<name>               CWL workflow file (TS15 only) replacing default file
    -psi=<name>               Text file with input product URLsfor workflow (TS15 only)
    -i=<name>                 Docker image identifier (URL)
                              Default value is automatically determined
    -a=<name>                 Docker authentication file (config.json)
                              Default value is automatically determined
    -n=<name>                 Test site name (parameter for cdab-client call)

ARGUMENTS
    <test-scenario>           Test scenario ID
                              Possible values: TS11, TS12, TS13, TS15.1, TS15.5

```

## Configuration

The **cdab-remote-client** uses a main YAML-compatible configuration in which reusable settings are stored. The file under */opt/cdab-remote-client/etc/config.yaml* shows initial settings for a small number of service providers. It is possible to specify a different file using the `-conf` option.

The file's structure consists of two main sections, one for **global** settings and another for the configuration of the various **service providers**.


### Global settings

In the **global section** some settings affecting the processing of all test scenarios can be configured.

* **docker_config**: The location of the Docker authentication file (*config.json*). This file is required to authenticate with Terradue's Docker repository to install the image for the testing on the virtual machine. This type of file can be obtained by running the following command and authenticating with the Terradue username and password.
  ```
  sudo docker login docker.terradue.com
  ```
* **ca_certificate**: The locations of CA certificate files to be copied onto the virtual machines used for docker repository authentication (to be specified as an array, like *['ca_file_1', 'ca_file_2']*)
* **connect_retries**: The number of retries for initial SSH connections after a virtual machine has been created and assigned an IP address.
* **connect_interval**: The interval between those initial SSH connections (in seconds, fraction are also possible).
* **max_retention_hours**: The maximum number of hours of runtime existing virtual machines are considered still in use. Virtual machine older than that are deleted before executing the tests. The default value is *6*.

### Settings for service providers

The **service_providers** section contains one subsection for each of the available service providers that can provide processing capacity or data catalogues or both. When the tool is run, the configurations are selected for execution based on the value of the `-sp` and `-ts` command-line options. They can refer to the same service provider.

A service provider section can contain two subsections: **compute** (for settings related to remote test execution) and **data** (for settings related to queryable target sites) and **storage** (for settings related object storage services).


#### Processing-related settings

These settings are configured in the **compute** section. The values configured here are used when the service provider name is used as the value of the `-sp` command-line option which specifies the cloud environment in which to run the test.

Currently the following APIs are supported and each have slightly different settings to configure:

* OpenStack (default)
* Google Cloud Platform
* Amazon AWS EC2


##### Common settings

* **private_key_file**: Location of the private key file for SSH connections to virtual machine (must correspond to public key in **key_name**).
* **remote_user**: User on virtual machine for SSH connections.
* **download_origin**: Value of the environment variable `DOWNLOAD_ORIGIN` for the Docker image execution on the virtual machine.
* **vm_name**: Preferred name of virtual machines to be created (sequential number is appended).
* **key_name**, **image_name**, **flavor_name**: these are mandatory for all types of providers, but differ among them, see the explanations in the individual sections.
* **cost_monthly**: Monthly cost for VM of specified flavour (instance type or machine type, see sections below). The default is *0*. If there is more than one flavour, the value has to be an array of the same size.
* **cost_hourly**: Hourly cost of VM of specified flavour. The default is *0*. If there is more than one flavour, the value has to be an array of the same size.
* **currency**: Payment currency. The default is *EUR*


##### Specific settings for OpenStack

Most of the values for the various keys can be obtained from the OpenStack dashboard of the cloud environment in question (by inspecting the file *clouds.yaml* that can be downloaded under *API Access > Download OpenStack RC File > OpenStack clouds.yaml File*), others have to be set with knowledge of concrete items that are configured on the cloud environment in question.

* **connector**: The value has to be *openstack* to which it defaults (making it optional in this case).
* **auth_url**: Authentication access point. Obtain value from `auth_url` key in *clouds.yaml*.
* **username**: Cloud username (same username as for access to the OpenStack dashboard).
* **password**: Cloud password (password for user).
* **project_id**: Project ID. Obtain value from `project_id` key in *clouds.yaml*. This setting is optional.
* **project_name**: Project name. Obtain value from `project_name` key in *clouds.yaml*.
* **user_domain_name**: User domain name. Obtain value from `user_domain_name` key in *clouds.yaml*.
* **region_name**: Authentication region name. Obtain value from `region_name` key in *clouds.yaml*. This setting is optional.
* **interface**: Interface. Obtain value from `interface` key in *clouds.yaml*.
* **identity_api_version**: Identity API version, Obtain value from `identity_api_version` key in *clouds.yaml*.
* **volume_api_version**: Volume API version (set value to *2* if version *3* is not supported).
* **key_name**: Name of predefined public key for SSH connection to new virtual machine. On the OpenStack dashboard, key pairs can be created on the OpenStack dashboard and the private key can be downloaded, check under *Compute > Key Pairs*.
* **image_name**: Name of image to be used for new virtual machine. On the OpenStack dashboard, choose from *Compute > Images*.
* **flavor_name**: Name of flavour (hardware characteristics) for new virtual machine. On the OpenStack dashboard, check under *Compute > Instances > Launch Instance > Flavours/Flavors*. The value can also be an array, e.g. *['flavourA', 'flavourB']*
* **network_name**: Name of network to which new virtual machine is connected. On the OpenStack dashboard, check under *Network > Networks*. This setting is optional. The value can also be an array, e.g. *['networkA', 'networkB']*
* **security_group**: Name of security group for new virtual machine (optional; this setting might be necessary in order to permit remote access to virtual machines). On the OpenStack dashboard, check under *Network > Security groups*, if available. This setting is optional.
* **floating_ip**: Explicitly assign floating IP (set this to *True* if public IP addresses are not assigned automatically at the creation of a virtual machine and otherwise to *False*). On the OpenStack dashboard, check under *Network > Floating IPs*, if available.
* **floating_ip_network**: Network from which to assign floating IP. This setting is optional.
* **private_key_file**: Location of the private key file for SSH connections to virtual machine (must correspond to public key in **key_name**).
* **remote_user**: User on virtual machine for SSH connections.
* **use_volume**: Create an external volume for docker image and test execution; this is useful for flavours that have very limited main disk. The size of the additional disk is 100 GB.
* **use_tmp_volume**: Create an external volume for the /tmp directory; this is useful for flavours that have very limited main disk. The size of the additional disk is 50 GB.

##### Specific settings for Google Cloud Platform

* **connector**: The value has to be set to *google*.
* **account_file**: Location of authentication key JSON file for the service account related to project. Service accounts are created via https://console.cloud.google.com/iam-admin/serviceaccounts and the file can be downloaded during the creation. *NOTE*: This setting can also be made
* **project_id**: Name of the project on the Google Cloud Platform.
* **region_name**: Zone, e.g. *europe-west2-c*.
* **key_name**: This setting is not applicable, as Google creates all accounts for which there are keys defined and authorises all keys. Key pairs are defined under https://console.cloud.google.com/compute/metadata/sshKeys.
* **image_name**: Fully qualified name of boot disk image to be used for new virtual machine, e.g. *projects/centos-cloud/global/images/centos-7-v20200309*.
* **flavor_name**: The machine type name name, e.g. *e2-standard-2*. Can also be specified, as in previous version, as fully qualified name of machine type for new virtual machine, e.g *projects/<project-name>/zones/europe-west2-c/machineTypes/e2-standard-2*.

##### Specific settings for Amazon AWS EC2

* **connector**: The value has to be set to *amazon*.
* **username**: Access key ID (a random combination of numbers and uppercase letters). Accounts and access keys are created under https://console.aws.amazon.com/iam/home?#/users.
* **password**: Secret access key associated with the access key ID (a longer random combination of numbers and letters and other characters).
* **region_name**: Region name, e.g. *eu-west-2*.
* **key_name**: Name of predefined public key for SSH connection to new virtual machine. Keys pairs can be defined from the EC2 console under *Key Pairs*.
* **image_name**: Image ID of the to be used (usually starts with *ami-*).
* **flavor_name**: Instance type of the machine (e.g. *t2.micro*).
* **security_group**: Name of security group for new virtual machine. There must be a security group defined to permit inbound SSH traffic.


#### Target site settings

These settings are configured in the **data** section. The values configured here are used when the service provider name is used as the value of the `-ts` command-line option. They regard URL and credentials for the target site on which to perform the search query.

* **url**: The endpoint URL of the target site.
* **credentials**: The access credentials for the target site in the format `username:password`.

These values are equivalent to the `-te` and `-tc` command-line options, respectively. If both `-te` and `-tc` are set, those values take precedence over the ones configured via the `-ts` option.



## Files

Remember that the **cdab-remote-client** relies on these additional files to run:

* The main configuration file (see above).
* Private key files for SSH connections to virtual machines.
* A Docker authentication file.
* For use of the Google Cloud Platform: an authentication key file.


## Processing

**cdab-remote-client** performs the following tasks:

* Check the command line arguments, configuration and selected test scenarios and starts the test making sure that there is no misconfiguration.
* The cloud environment to be used is obtained from the value of the `-sp` option which determines the service provider section in the configuration file to be used (values are taken from its **compute** subsection).
* The target site parameters to be used are obtained from the value of the `-te` and `tc` options. Alternatively the `-ts` option is used; it determines the service provider section in the configuration file to be used (values are taken from its **data** subsection).
* If configured via the **floating_ip** key in the main configuration file, get the list of available floating IP addresses and make sure they are sufficient to perform all tests in parallel.
* Delete old virtual machines no longer in use according to the **max_retention_hours** global setting.
* Start a new thread for each requested virtual machine (`-vm` option) and do the following in parallel for each:
 
  * Create the virtual machine (using the `openstack server create` command or an equivalent for other providers)
  * If configured via the **floating_ip** key in the main configuration file, assign a floating IP address to the virtual machine (using the `openstack server add floating ip` command or an equivalent for other providers if applicable).
  * If configured via the **use_volume** key, create and attach the volume to the virtual machine (using the `openstack volume create` and `openstack server add volume` commands or equivalents for other providers if applicable) and partition, format and mount the volume.
  * Install Docker and start the Docker service (in case the key **use_volume** was set to *True*, change the local docker repository location to the new volume.
  * Transfer the Docker authentication file in order to be able to authenticate with the Terradue Docker repository.
  * Install the testing suite image containing the **cdab-client** tool (or other images).
  * Run the test scenario based on the command-line arguments, configuration settings and mapping of remote test scenarios onto **cdab-client** scenarios or other testing executables.
  * After conclusion extract the result files (*TS\*Results.json* and *junit.xml*) from the Docker container and download it.
  * If configured via the **use_volume** key, detach the volume from the virtual machine and delete it (using the `openstack server remove volume` and `openstack volume delete` commands or equivalents for other providers if applicable).
  * Delete the virtual machine (using the `openstack server delete` command or an equivalent for other providers).

* When all threads have completed, calculate the metrics described above and produce a *TS\*Results.json* file containing the information about the executed test scenario and an updated *junit.xml*.

The entire execution should take only a few minutes. 

## Specific processing for EO data and Amazon S3

In order to use a different additional network connections for access to EO data, in the **compute** section of the YAML configuration file, specify a list of networks for the **network_name** key (using the array syntax). The keys **project_name** and **project_id** might also change.

For Amazon S3 data access, make sure that the **download_origin** key has the value *eocloud.eu* and that the **data** section for the used target sites contains the appropriate access keys etc.


## Debugging

During the execution, **cdab-remote-client** prints logging basic information about the steps described in the previous section. The `-v` flag enables more detailed logging containing executed commands and other information.

Note that when more than one virtual machine is requested (`-vm` option), logging and debugging information is only available once all threads have finished and are not grouped by virtual machine for readability.
