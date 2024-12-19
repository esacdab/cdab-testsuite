# pycdab Python client 

This client, **pycdab**, is a simplified re-implementation of the cdab-client (also in this repository) for running the test scenarios TS01 and TS02 against Copernicus Dataspace Environment.
This is to demonstrate that the CDAB can be which may be easier to use and maintain than the existing cdab-client which was developed in C#.
**pycdab** produces the metrics known from the cdab-client in output files of the same name (e.g. `TS01Results.json`).


## Installation

Install the dependencies using this command (if desired, in a dedicated virtual environment).

Make a copy of the file config.yaml.tpl and name it config.yaml. Set your Copernicus Dataspace credentials under `service_providers.cdse.data.credentials`


## How to run

The usage is as follows:
```
Usage: pycdab [-v] --conf=<config-file> --sp=<service-provider> <test-scenario> [--lf=<load-factor>] 
              
Test scenarios: TS01 | TS02
```

### Examples:

Run the test scenario TS01 (simple data search and single download) with a single query and download:

```python3 src/pycdab/pycdab.py -v --conf=config.yaml --sp=cdse TS01```

Run the test scenario TS01 (complex data search and bulk download) with 4 simultaneous queries and downloads:

```python3 src/pycdab/pycdab.py -v --conf=config.yaml --sp=cdse --lf=4 TS02```