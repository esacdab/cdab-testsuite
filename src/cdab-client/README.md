# CDAB Test Client

Test Scenarios are called using the cdab-client program from the command line in the container. 

## Usage

The main parameters are shown on the help page.

Type

```
cdab-client -h
```

And the output is the following:

```
Usage: cdab-test [TEST SCENARIOS]+
Launch one or more test scenarios to a target site
If no test scenario is specified, the generic TS01 is executed.

Options:
      -conf=VALUE           YAML file containing the configuration. (Default; config.yaml in current dir)
      -tu, --target_url=VALUE
                             target endpoint URL (default https://scihub.copernicus.eu/apihub). Overrides configuration file.
      -tc, --target_credentials=VALUE
                             the target credentials string (e.g. username:password). Overrides configuration file.
      -tn, --target_name=VALUE
                             the target identifier string. Mandatory to use the target site configuration from file.
      -tsn, --testsite_name=VALUE
                             the test site identifier for reporting.
      -gid, --group_id=VALUE
                             Test group ID used to identify concurrent tests. If not provided a uuid is generated.
      -lf, --load_factor=VALUE
                             Load Factor. Mainly used as a constant to calculate the number of test unit to make per test cases
      -mp, --max_parallelism=VALUE
                             Max Parallelism. Maximum number of concurrent requests per test cases
      -vm=VALUE              Number of virtual machines to be run in parallel (min: 1). Default value: 1
      -sp=VALUE              Service provider for test execution (as defined in configuration file)
      -c=<name>              Docker container identifier. Default value is automatically determined
      -v                     increase debug message verbosity.
      -h, --help             show this message and exit.

ARGUMENTS
    <test-scenario>           Test scenario ID. Possible values: TS01, TS02, TS03, TS04, TS05, TS06, TS11, TS12
```

## Configuration

The **cdab-client** uses a main YAML-compatible configuration in which reusable settings are stored. The file *config.sample.yaml* shows initial settings for a small number of service providers. It is possible to specify this file using the `-conf` option.

The file's structure consists of two main sections, one for **global** settings and another for the configuration of the various **service providers**.

### Global settings

In the **global section** you will find the following settings:

* `docker_config` is the location of the Docker authentication file (*config.json*). This file is required to authenticate with Terradue's Docker repository to install the image for the testing on the virtual machine. This type of file can be obtained by running the following command and authenticating with the Terradue username and password.
```
sudo docker login docker.terradue.com
```
* `reference_target_site` is the service provider name (see next section) used as the reference for the catalogue comparison test scenarios. In TS05 and TS06, the coverage ratio is calculated based on this service provider.

### Settings for service providers

The **service_providers** section contains one subsection for each of the available service provider that can provide processing capacity or data catalogues or both. When the tool is run, the configurations are selected for execution based on the value of the `-sp` and `-tn` command-line options. They can refer to the same service provider.

A service provider section can contain two subsections: **compute** (for settings related to remote test execution) and **data** (for settings related to queryable target sites).

#### Computing-related settings

These settings are configured in the **compute** section. The values configured here are used when the service provider name is used as the value of the `-sp` command-line option which specifies the cloud environment in which to run the test.

Most of the values for the various keys can be obtained from the OpenStack dashboard of the cloud environment in question (by inspecting the file *clouds.yaml* that can be downloaded under *API Access > Download OpenStack RC File > OpenStack clouds.yaml File*), others have to be set with knowledge of concrete items that are configured on the cloud environment in question.

* **auth_url**: Authentication access point (obtain value from `auth_url` key in *clouds.yaml*).
* **username**: Cloud username (same username as for access to the OpenStack dashboard).
* **password**: Cloud password (password for user).
* **project_id**: Project ID (obtain value from `project_id` key in *clouds.yaml*). This setting is optional.
* **project_name**: Project name (obtain value from `project_name` key in *clouds.yaml*).
* **user_domain_name**: User domain name (obtain value from `user_domain_name` key in *clouds.yaml*).
* **region_name**: Authentication region name (obtain value from `region_name` key in *clouds.yaml*). This setting is optional.
* **interface**: Interface (obtain value from `interface` key in *clouds.yaml*).
* **identity_api_version**: Identity API version (obtain value from `identity_api_version` key in *clouds.yaml*).
* **vm_name**: Preferred name of virtual machines to be created (sequential number is appended).
* **key_name**: Name of predefined public key for SSH connection to new virtual machine (key pairs can be created on the OpenStack dashboard and the private key can be downloaded, check under *Compute > Key Pairs* on the OpenStack dashboard).
* **image_name**: Name of image to be used for new virtual machine (choose from *Compute > Images* on the OpenStack dashboard).
* **flavor_name**: Name of flavour for new virtual machine (check under *Compute > Instances > Launch Instance > Flavours/Flavors* on the OpenStack dashboard).
* **security_group**: Name of security group for new virtual machine (optional; this setting might be necessary in order to permit remote access to virtual machines, check under *Network > Security groups* on the OpenStack dashboard, if available). This setting is optional.
* **floating_ip**: Explicitly assign floating IP (set this to *True* if public IP addresses are not assigned automatically at the creation of a virtual machine and otherwise to *False*, check under *Network > Floating IPs* on the OpenStack dashboard, if available).
* **private_key_file**: Location of the private key file for SSH connections to virtual machine (must correspond to public key in **key_name**).
* **remote_user**: User on virtual machine for SSH connections.

#### Target site settings

These settings are configured in the **data** section. The values configured here are used when the service provider name is used as the value of the `-tn` command-line option. They regard URL and credentials for the target site on which to perform the search query.

* **url**: The endpoint URL of the target site.
* **credentials**: The access credentials for the target site in the format `username:password`.

These values are equivalent to the `-tu` and `-tn` legacy command-line options, respectively. If both `-tu` and `-tc` are set, those values take precedence over the ones configured via the `-tn` option.


## Files

Remember that the **cdab-client** relies on these additional files to run:

* The main configuration file (see above).
* A private key file for SSH connections to the virtual machine.
* A Docker authentication file.

## Virtual Machine provisioning

In case a compute service is requested via the `-sp` arguments **cdab-client** performs the following tasks to perform the test scenarios.

* Check the command line arguments, configuration and selected test scenarios and starts the test making sure that there is no misconfiguration.
* The cloud environment to be used is obtained from the value of the `-sp` option which determines the service provider section in the configuration file to be used (values are taken from its **compute** subsection).
* The target site parameters to be used are obtained from the value of the `-tu` and `tc` options. Alternatively the `-tn` option is used; it determines the service provider section in the configuration file to be used (values are taken from its **data** subsection).
* If configured via the **floating_ip** key in the main configuration file, get the list of available floating IP addresses and make sure they are sufficient to perform all tests in parallel.
* Start a new thread for each requested virtual machine (`-vm` option) and do the following in parallel for each:
 
  * Create the virtual machine (using the `openstack server create` command)
  * If configured via the **floating_ip** key in the main configuration file, assign a floating IP address to the virtual machine (using the `openstack server add floating ip` command).
  * Install Docker and start the Docker service.
  * Transfer the Docker authentication file in order to be able to authenticate with the Terradue Docker repository.
  * Install the testing suite image containing the **cdab-client** tool.
  * Run the test scenario based on the command-line arguments, configuration settings and mapping of remote test scenarios onto **cdab-client** scenarios.
  * After conclusion extract the result files (*TS\*Results.json* and *junit.xml*) from the Docker container and download it.
  * Delete the virtual machine (using the `openstack server delete` command).

* When all threads have completed, calculate the metrics described above and produce a *TS\*Results.json* file containing the information about the executed test scenario and an updated *junit.xml*.

The entire execution should take only a few minutes. 
