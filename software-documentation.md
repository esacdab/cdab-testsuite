# Software repository

## General

The purpose of the CDAB software suite is to allow users to perform automated tests on DIAS.

The test scenarios that can be executed are listed below in this document.

There are two tools test

* **cdab-client**, a .NET/mono based tool to perform test scenarios TS01 to TS07 locally on the machine from which they run.
* **cdab-remote-client**, a Python-based tool to perform test scenarios TS11 to TS15, which require a virtual machine on the provider.

## Prerequisites

The software can be used on all modern versions of **Linux**, **macOS** and **Windows**.

For **cdab-client**

You need to install the latest version of **Mono**, the cross-platform open-source .NET framework.

For an efficient use of the tool it is recommended to use **Visual Studio Code** with the following extensions:
* C#
* Mono Debug
* .NET Core TestExplorer


For **cdab-remote-client**

You need **Python** (at least version 3.6) and the Python package *python-openstackclient* (to be able to use the OpenStack interfaces of the DIAS providers).


## Configuration

Both tools rely on a configuration file in YAML format in which information about the various providers is configured. The YAML format is hierarchical and
The software suite contains a sample configuration that is ready to use apart from the user credentials which have to be replaced with correct ones.


The following sections explain he configuration which consists of two main nodes:

* global: Contains settings that apply globally, over different service providers and test scenarios.
* service_providers: Contains specific sections for individual DIASes.


### `global` node

The following settings are related to catalogue searches and apply across providers.

* `reference_target_site`: 
* `country_shapefile_path`: Path to the shapefile containing country borders; used for making queries for coutry coverage.

The following settings are related to processing-related test scenarios and apply across providers.

* `docker_config`: Docker configuration file to be used for docker repository connections on virtual machine.
* `connect_retries`: Number of attempts to connect to virtual machines via SSH.
* `connect_interval`: Interval between attempts to connect to virtual machines via SSH (in seconds, fraction also possible).
* `ca_certificate`: An array of optional certificates to be installed on virtual machines used for processing.
* `max_retention_hours`: The number of hours after which previously created virtual machines are considered idle (in case they were not deleted properly during test execution, check takes place before creating a new VM on a service provider).



### `service providers` section

Under this node many different configurations for DIASes or similar providers can be attached. Each of them has a root node that should be named after the provider. Under that root node there are several keys for settings related to that provider.

* `max_catalogue_thread`: Maximum number of thread querying the target catalogue service in parallel.
* `max_download_thread`: Maximum number of thread downloading the target download service in parallel.


* `data`: A node with further settings related to catalogue search and download (see below).
* `compute`: A node with further settings related to remote execution of test scenarios (see below).
* `storage`: A node with further settings related to storage of user-produced data (see below).

The following sections explain the more complex nodes more in detail.


#### `data` node

These settings regard the data offering of each provider. They can be complicated, especially the definition of collections. It is recommeded to use sample configuration file the and adjust only the credentials. These settings are relevant for all scenarios that perform searches or downlooads.

TODO Explain settings

#### `compute` node

These settings configure the processing within the cloud infrastructure of the service providers. They are relevant for TS11, TS12, TS13 and TS15.

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
* **use_volume**: Create an external volume for docker image and test execution; this is useful for flavours that have very limited main disk. The size of the additional disk is 20 GB.
* **private_key_file**: Location of the private key file for SSH connections to virtual machine (must correspond to public key in **key_name**).
* **remote_user**: User on virtual machine for SSH connections.
* **download_origin**: Value of the environment variable `DOWNLOAD_ORIGIN` for the Docker image execution on the virtual machine.
* **vm_name**: Preferred name of virtual machines to be created (sequential number is appended).
* **key_name**, **image_name**, **flavor_name**: these are mandatory for all types of providers, but differ among them, see the explanations in the individual sections.
* **cost_monthly**: Monthly cost for VM of specified flavour (instance type or machine type, see sections below). The default is *0*. If there is more than one flavour, the value has to be an array of the same size.
* **cost_hourly**: Hourly cost of VM of specified flavour. The default is *0*. If there is more than one flavour, the value has to be an array of the same size.
* **currency**: Payment currency. The default is *EUR*


#### `storage` node

These settings configure storage access for the service providers. They are relevant for TS07.

TODO Explain settings


## Test scenarios

The test scenarios execute a sequence of basic test cases which are explained in the following section

The following table shows the test scenarios that access the service provider from the user's machine. They are run using **cdab-client**.

Scenario ID | Title | Test case sequence
TS01 | Simple data search and single download | TC101 → TC201 → TC301
TS02 | Complex data search and bulk download |  TC101 → TC202 → TC302
TS03 | Systematic periodic data search and related remote data download | TC203 → TC303
TS04 | Offline data download | TC204 → TC304
TS05 | Data Coverage Analysis | TC501 → TC502
TS06 | Data Latency Analysis | TC601 → TC602
 TS07 | Storage Upload and Download Performance | TC701 → TC702

The following table shows the test scenarios that run on virtual machines within the service providers' cloud infrastructure. They are run using **cdab-remote-client**.

| Scenario ID | Title | Test cases |
| TS11 | Cloud services simple local data search and single local download on single virtual machines | TC411 (→ #TC211 → #TC311) |
| TS12 | Cloud services complex local data search and multiple local download on multiple virtual machines | #TC412 (→ #TC212 → #TC312) |
| TS13 | Cloud services simple local data search, download and simple processing of downloaded data | TC413 |
| TS15 | Cloud services processing of specific workflows | TC415 (→ TC416) |

Test scenario 15 (TS15) covers several end-to-end scenarios which are independent of each other.


## Test cases

The following sections give a short overview about the simple test cases and how they can be configured and run.

### TC101: Service Reachability

### TC201: Basic query

### TC202: Complex query (geo-time filter)

### TC203: Specific query (handle multiple results pages)

### TC204: Offline data query

### TC211: Basic query from cloud services

### TC212: Complex query from cloud services

### TC301: Single remote online download

### TC302: Multiple remote online download

### TC303: Remote Bulk download

### TC304: Remote Bulk download

### TC311: Single remote online download from cloud services

### TC312: Multiple remote online download from cloud services

### TC411: Cloud Services Single Virtual Machine Provisioning

### TC412: Cloud Services Multiple Virtual Machine Provisioning

### TC413: Cloud Services Virtual Machine Provisioning for Processing

### TC415: Automated Processing of End-to-End Scenario of Specific Applications

### TC501: Catalogue Coverage

### TC502: Local Data Coverage

### TC503: Data Offer Consistency

### TC601: Data Operational Latency Analysis [Time Critical]

### TC602: Data Availability Latency Analysis

### TC701: Data Storage Upload Analysis

### TC702: Data Storage Download Analysis


# Usage and processing steps of the tools

## cdab-client

TODO usage, arguments

**cdab-remote-client** performs the following steps:

TODO:
...

## cdab-remote-client

TODO usage, arguments

**cdab-remote-client** performs the following steps:

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

