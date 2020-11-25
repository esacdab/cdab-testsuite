# Use Case Scenario #2 - Rapid mapping - Integration Procedures

## Development Environment installation procedure

1. Provision a virtual machine on the target site. Preferably with the following specification
    - 1 CPU, 8GB RAM, 100GB disk
    - CentOS 7
    - Jupyter Notebook (with Python 3 support)
    - With data offer access if required
  
2. Open Jupyter Notebook on the provisioned machine.

3. Run the following in a cell and make sure there is no error.

```python
import lxml.etree as etree
import snappy 
from snappy import GPF
import logging
```

4. If there is an error and you have sufficient privileges on the machine you are working on, transfer the the included file _environment.yml_ there and create a new conda environment and activate that environment using this command:
```console
$ conda env create --file environment.yml
...
$ conda activate env_s3
```

## Integration procedure 

1. Open Jupyter Notebook on the virtual machine or within the environment provided through the target site's web interface. [10%]

2. Upload the files scenario code files (_burned\_area.ipynb_ and the two helper _*.py_ files) to the workspace folder using the Jupyter upload functionality. [20%]

3. **Using the target site data access and following the documentation available at the target site**, get a relevant Sentinel-3 SLSTR product tile. For instance, the product with the identifier `S2 L2A` of the `TBD` (Portugal) [30%]

If the product is not available from the target site, you can download it from the Terradue storage.

```console
$ curl -L ... TBD"
```

Make sure the contents of the zipped archive are extracted and available and lolcated in a directory accessible by Jupyter Notebook (adjust the notebook cell under *Data location and properties* as required). [40%]

```console
$ unzip S2 TBD
```
4. Return to Jupyter Notebook, open the notebook with a Python 3 kernel. If you created and new conda environment during the installation procedure, make sure the Python kernel is using that environment. [50%]

5. Execute, one after another, the cells of the notebook, waiting for each cell to complete, ensuring no errors occur. [60%]

6. In the directory of your notebook there should now be a GeoTIFF file named `TBD` [90%]

```console
$ ls -l /workspace/TBD
-rw-r--r-- 1 user ciop 7946390 Sep 23 14:41 /workspace/TBD
```

8. Download it to your computer and open it with any tool that can visualise TIFF files. Verify that the band/layer `TBD` show a monochrome image of the detected fires as in the picture below: [100%]



## Application build procedure 

There is no systematically run application for this scenario, so no application and container need to be built.