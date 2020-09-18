# Use Case Scenario #2 - Rapid mapping - Integration Procedures

## Development Environment installation procedure

1. Provision a virtual machine on the target site. Preferably with the following specification
    - 1 CPU, 8GB RAM, 100GB disk
    - CentOS 7
    - Jupyter Notebook (with Python 3 support)
    - With data offer access if required
  
2. Open Jupyter Notebook on the provisioned machine

3. Run the following in a cell and make sure there is no error.

```python

```

4. If there is an error, try to create a new environment based on the following file (_environment.yml_):
```yaml

```
Using the following command:


## Integration procedure 

1. Open the Jupyter notebook on the virtual machine. [10%]

2. Upload the files scenario code files (_rapid-mapping.ipynb_ and _*.py_) to the workspace folder using the Jupyter upload functionality. [20%]

3. **Using the target site data access and following the documentation available at the target site**, get a relevant Sentinel-3 SLSTR product tile. For instance, the product with the identifier `S3A_SL_1_RBT____20190826T140456_20190826T140756_20190827T175944_0179_048_281_3060_LN2_O_NT_003` of the `26 Aug 2019 14:04:56 GMT` [30%]

```console
$ curl -L -o S3A_SL_1_RBT____20190826T140456_20190826T140756_20190827T175944_0179_048_281_3060_LN2_O_NT_003.zip "https://store.terradue.com/download/sentinel3/files/v1/S3A_SL_1_RBT____20190826T140456_20190826T140756_20190827T175944_0179_048_281_3060_LN2_O_NT_003"
```

Make sure the contents of the zipped archive are extracted and available in the directory used by the notebook (adjust the notebook cell under *Data location and properties* as required). [40%]

```console
$ unzip S3A_SL_1_RBT____20190826T140456_20190826T140756_20190827T175944_0179_048_281_3060_LN2_O_NT_003.zip
```

5. Execute, one after another, the cells of the notebook, waiting for each cell to complete, ensuring no errors occur. [60%]

6. In the same directory with the bands images, you should find a `???` file with name ending with `???` [90%]

```console
$ ls -l /tmp/S2A_MSIL1C_20200507T104031_N0209_R008_T31TFK_20200507T124549.SAFE/GRANULE/L1C_T31TFK_A025459_20200507T104558/IMG_DATA/
...
T31TFK_20200507T104031_NDVI.tif
```

8. Download it to your computer and open it with any tool that can visualise TIFF files. [100%]

???

## Application build procedure 

???

1. Open a terminal on the previously set-up virtual machine

2. Go to the Use case folder

3. Launch docker build prefixing the name with the target site docker hub repository (here docker.terradue.com)

```console
$ docker build -t docker.terradue.com/cdab-ndvi .
```

4. Push the docker to the hub

```console
$ docker push docker.terradue.com/cdab-ndvi
```