# pycdab Python client 

This client, **pycdab**, is a simplified re-implementation of the cdab-client (also in this repository) for running the test scenarios TS01 and TS02 against Copernicus Dataspace Environment.
This is to demonstrate that the CDAB can be implemented fully in Python which may be easier to use and maintain than the existing cdab-client which was developed in C#.
**pycdab** produces the metrics known from the cdab-client in output files of the same name (e.g. `TS01Results.json`).


## Installation

Install the dependencies using this command (if desired, in a dedicated virtual environment).

```
pip3 install loguru requests pyyaml
```

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

Run the test scenario TS02 (complex data search and bulk download) with 4 simultaneous queries and downloads:

```python3 src/pycdab/pycdab.py -v --conf=config.yaml --sp=cdse --lf=4 TS02```

The output looks as follows:

```
$ python3 src/pycdab/pycdab.py -v --conf=src/pycdab/config.yaml --sp=cdse --lf=2 TS01
2024-12-19 19:32:37.620 | INFO     | test_cases.test_case:prepare:26 - Preparing test TC101 (Service reachability)
2024-12-19 19:32:37.621 | INFO     | test_cases.test_case:run_single:206 - TC101 Thread #1 start
2024-12-19 19:32:37.621 | INFO     | test_cases.test_case:run_single:206 - TC101 Thread #2 start
2024-12-19 19:32:38.246 | INFO     | test_cases.test_case:run_single:212 - TC101 Thread #1 end
2024-12-19 19:32:38.384 | INFO     | test_cases.test_case:run_single:212 - TC101 Thread #2 end
2024-12-19 19:32:38.384 | INFO     | test_cases.test_case:prepare:26 - Preparing test TC201 (Basic catalogue query)
2024-12-19 19:32:38.385 | INFO     | test_cases.test_case:run_single:241 - TC201 Thread #1 start
2024-12-19 19:32:38.385 | INFO     | connectors.cdse:query:59 - [Thread #1] Filter: Collection/Name eq 'Sentinel-5P' and Attributes/OData.CSC.StringAttribute/any(a:a/Name eq 'processingLevel' and a/OData.CSC.StringAttribute/Value eq 'L2') and Attributes/OData.CSC.StringAttribute/any(a:a/Name eq 'processingMode' and a/OData.CSC.StringAttribute/Value eq 'REPROCESSING')
2024-12-19 19:32:38.386 | INFO     | test_cases.test_case:run_single:241 - TC201 Thread #2 start
2024-12-19 19:32:38.387 | INFO     | connectors.cdse:query:59 - [Thread #2] Filter: Collection/Name eq 'SENTINEL-1' and Attributes/OData.CSC.StringAttribute/any(a:a/Name eq 'productType' and a/OData.CSC.StringAttribute/Value eq 'OCN')
2024-12-19 19:32:38.744 | INFO     | test_cases.test_case:run_single:245 - TC201 Thread #1 end
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151230T194648_20151230T194748_009276_00D639_469A.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151024T195507_20151024T195607_008299_00BB30_A9B7.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151024T195607_20151024T195707_008299_00BB30_2194.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151024T195403_20151024T195507_008299_00BB30_9A02.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151228T084707_20151228T084807_009240_00D53D_93E6.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151221T201129_20151221T201229_009145_00D284_2565.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151221T201229_20151221T201329_009145_00D284_3CDF.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151220T193119_20151220T193219_009130_00D20F_E7A4.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SSV_20151230T040157_20151230T040302_009266_00D5FC_D490.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SSV_20151230T040402_20151230T040502_009266_00D5FC_A6C4.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151229T092851_20151229T092951_009255_00D5AD_8C34.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151230T194544_20151230T194648_009276_00D639_1CFB.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SSV_20151230T040502_20151230T040548_009266_00D5FC_1E0B.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151229T092951_20151229T093037_009255_00D5AD_72F2.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151228T200414_20151228T200514_009247_00D56D_5DF4.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151228T200210_20151228T200314_009247_00D56D_BFD9.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151228T084807_20151228T084907_009240_00D53D_FF98.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151225T193916_20151225T194016_009203_00D42C_331D.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151225T194016_20151225T194114_009203_00D42C_94EB.SAFE
2024-12-19 19:32:38.957 | DEBUG    | connectors.cdse:query:80 - [Thread #2] S1A_EW_OCN__2SDH_20151224T092125_20151224T092225_009182_00D392_F5A3.SAFE
2024-12-19 19:32:38.957 | INFO     | test_cases.test_case:run_single:245 - TC201 Thread #2 end
2024-12-19 19:32:38.957 | INFO     | test_cases.test_case:prepare:26 - Preparing test TC301 (Single remote download)
2024-12-19 19:32:38.958 | DEBUG    | test_cases.test_case:prepare:331 - Selecting random item from results of previous test case
2024-12-19 19:32:38.958 | INFO     | test_cases.test_case:prepare:334 - Item for download: S1A_EW_OCN__2SDH_20151225T193916_20151225T194016_009203_00D42C_331D.SAFE
2024-12-19 19:32:38.958 | INFO     | test_cases.test_case:run_single:342 - TC301 Download thread start
2024-12-19 19:32:42.836 | DEBUG    | connectors.cdse:get_access_token:196 - [{0}] Access token obtained
2024-12-19 19:32:42.836 | INFO     | connectors.cdse:download:155 - [Download thread] Download URL: https://catalogue.dataspace.copernicus.eu/odata/v1/Products(3464d91e-59a9-5294-bc30-6b9358abfb21)/$value
2024-12-19 19:32:44.118 | INFO     | connectors.cdse:download:164 - [Download thread] Download size: 35053694
2024-12-19 19:32:44.717 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 10485760 bytes (29.91%)
2024-12-19 19:32:44.728 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 11534336 bytes (32.9%)
2024-12-19 19:32:44.738 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 12582912 bytes (35.9%)
2024-12-19 19:32:44.747 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 13631488 bytes (38.89%)
2024-12-19 19:32:44.757 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 14680064 bytes (41.88%)
2024-12-19 19:32:44.767 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 15728640 bytes (44.87%)
2024-12-19 19:32:44.777 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 16777216 bytes (47.86%)
2024-12-19 19:32:44.786 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 17825792 bytes (50.85%)
2024-12-19 19:32:44.796 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 18874368 bytes (53.84%)
2024-12-19 19:32:44.805 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 19922944 bytes (56.84%)
2024-12-19 19:32:44.816 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 20971520 bytes (59.83%)
2024-12-19 19:32:44.825 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 22020096 bytes (62.82%)
2024-12-19 19:32:44.835 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 23068672 bytes (65.81%)
2024-12-19 19:32:45.333 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 24117248 bytes (68.8%)
2024-12-19 19:32:45.335 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 25165824 bytes (71.79%)
2024-12-19 19:32:45.395 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 26214400 bytes (74.78%)
2024-12-19 19:32:45.396 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 27262976 bytes (77.77%)
2024-12-19 19:32:45.435 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 28311552 bytes (80.77%)
2024-12-19 19:32:45.455 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 29360128 bytes (83.76%)
2024-12-19 19:32:45.924 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 30408704 bytes (86.75%)
2024-12-19 19:32:45.983 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 31457280 bytes (89.74%)
2024-12-19 19:32:45.985 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 32505856 bytes (92.73%)
2024-12-19 19:32:45.986 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 33554432 bytes (95.72%)
2024-12-19 19:32:46.027 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 34603008 bytes (98.71%)
2024-12-19 19:32:46.032 | DEBUG    | connectors.cdse:download:172 - [Download thread] Downloaded: 35053694 bytes (100.0%)
2024-12-19 19:32:46.032 | DEBUG    | connectors.cdse:download:180 - [Download thread] Download finished: 35053694 bytes
2024-12-19 19:32:46.032 | INFO     | test_cases.test_case:run_single:345 - TC301 Download thread end
```

The corresponding results file `TS01Results.json` is this:

```json
{
  "testCaseResults": [
    {
      "testName": "TC101",
      "className": "TestCase101",
      "startedAt": "2024-12-19T18:32:37.621076Z",
      "endedAt": "2024-12-19T18:32:38.384952Z",
      "duration": 764,
      "metrics": [
        {
          "name": "avgResponseTime",
          "value": 694,
          "uom": "ms"
        },
        {
          "name": "peakResponseTime",
          "value": 763,
          "uom": "ms"
        },
        {
          "name": "errorRate",
          "value": 0.0,
          "uom": "%"
        },
        {
          "name": "avgConcurrency",
          "value": 1.819,
          "uom": "#"
        },
        {
          "name": "peakConcurrency",
          "value": 2,
          "uom": "#"
        }
      ]
    },
    {
      "testName": "TC201",
      "className": "TestCase201",
      "startedAt": "2024-12-19T18:32:38.385192Z",
      "endedAt": "2024-12-19T18:32:38.957934Z",
      "duration": 573,
      "metrics": [
        {
          "name": "avgResponseTime",
          "value": 570,
          "uom": "ms"
        },
        {
          "name": "peakResponseTime",
          "value": 570,
          "uom": "ms"
        },
        {
          "name": "errorRate",
          "value": 50.0,
          "uom": "%"
        },
        {
          "name": "avgConcurrency",
          "value": 1.0,
          "uom": "#"
        },
        {
          "name": "peakConcurrency",
          "value": 1,
          "uom": "#"
        },
        {
          "name": "avgSize",
          "value": 63307,
          "uom": "bytes"
        },
        {
          "name": "maxSize",
          "value": 63307,
          "uom": "bytes"
        },
        {
          "name": "totalSize",
          "value": 63307,
          "uom": "bytes"
        },
        {
          "name": "totalReadResults",
          "value": 20,
          "uom": "#"
        },
        {
          "name": "resultsErrorRate",
          "value": 0.0,
          "uom": "%"
        },
        {
          "name": "maxTotalResults",
          "value": -1,
          "uom": "#"
        },
        {
          "name": "dataCollectionDivision",
          "value": [
            "Sentinel-5P L2 NRT last 1M Online"
          ],
          "uom": "string"
        }
      ]
    },
    {
      "testName": "TC301",
      "className": "TestCase301",
      "startedAt": "2024-12-19T18:32:38.958198Z",
      "endedAt": "2024-12-19T18:32:46.032670Z",
      "duration": 7074,
      "metrics": [
        {
          "name": "avgResponseTime",
          "value": 3196,
          "uom": "ms"
        },
        {
          "name": "peakResponseTime",
          "value": 3196,
          "uom": "ms"
        },
        {
          "name": "errorRate",
          "value": 50.0,
          "uom": "%"
        },
        {
          "name": "avgConcurrency",
          "value": 1.0,
          "uom": "#"
        },
        {
          "name": "peakConcurrency",
          "value": 1,
          "uom": "#"
        },
        {
          "name": "avgSize",
          "value": 35053694,
          "uom": "bytes"
        },
        {
          "name": "maxSize",
          "value": 35053694,
          "uom": "bytes"
        },
        {
          "name": "totalSize",
          "value": 35053694,
          "uom": "bytes"
        },
        {
          "name": "throughput",
          "value": 10968631.143,
          "uom": "bytes/second"
        },
        {
          "name": "dataCollectionDivision",
          "value": [
            "S1A_EW_OCN__2SDH_20151225T193916_20151225T194016_009203_00D42C_331D.SAFE starting at 2024-12-19T18:32:42Z ending at 2024-12-19T18:32:46Z"
          ],
          "uom": "string"
        },
        {
          "name": "dataAccess",
          "value": [
            "http"
          ],
          "uom": "string"
        }
      ]
    }
  ]
}
```