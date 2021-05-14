# Automated user benchmarking of Sentinels data access performances

## Script
The proposed script `automate.py` allows to automate the execution of the user tests available from the Copernicus Sentinels Data Access Benchmarking SW Suite [cdab-client](https://github.com/esa-cdab/cdab-testsuite/wiki/Command-Line-Tools#cdab-client).

## Registering to SciHub
To ensure that the execution of the `cdab-client` works as expected a SciHub account must be provided, after the [registration](https://scihub.copernicus.eu/dhus/#/self-registration) phase is completed modify the [`config.yaml`](https://github.com/esa-cdab/cdab-testsuite/blob/master/src/cdab-client/config.sample.yaml) with the email and password you used.
Further explanation on how to prepare the configuration is provided in the official [documentation](https://github.com/esa-cdab/cdab-testsuite/wiki#prepare-the-test-suite-configuration-configyaml).

## Working with other DIAS
To ensure that the execution of the script works correctly with other DIAS the `config.yaml` file has to be modified with the appropriate usernames and passwords.

## Installation
If Docker SDK is not installed run the following command `pip install docker`, then copy the repository, run the Docker client and the script with the command `python3 automate.py`.

## Configuration
A `config.json` file is provided and has to be placed in the same directory as the script file, the following parameters are available\
* containername: sets the name for the container that will be created
* scenarios: test scenarios to be run, the possible values are "TS01", "TS02", "TS03", "TS04", "TS05", "TS06", "TS07"
* config: the path to the [config.yaml](https://github.com/esa-cdab/cdab-testsuite/blob/master/src/cdab-client/config.sample.yaml) file needed by the `cdab-client`, if `null` the script will look for it in the current directory.
* targets: target sites where the test scenarios specified will be run, the possible values are "SciHub", "CREO", "ONDA", "MUNDI", "SOBLOO"

## Output
The results will be saved as tar files in the current directory with the following naming convention targetsite_scenario.tar and the container will be removed after the execution of the last scenario.

## Credits
This work has been performed in the frame of an internship at the European Space Agency