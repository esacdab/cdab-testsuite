# Use Case Scenario #2 - Rapid mapping - Integration Procedures

## Development Environment installation procedure

1. Provision a virtual machine on the target site. Preferably with the following specification
    - 1 CPU, 8GB RAM, 100GB disk
    - CentOS 7
    - Jupyter Notebook (with Python 3 support)
    - With data offer access if required
  
2. Open Jupyter Notebook on the provisioned machine.

3. Run the following in a cell and make sure there is no error (set the environment name and path correctly in the `PREFIX` variable).

```python
import os
os.environ['PREFIX'] = '/opt/anaconda/envs/env_burned_area/'
import sys
sys.path.append(os.path.join(os.environ['PREFIX'], 'conda-otb/lib/python'))
os.environ['OTB_APPLICATION_PATH'] = os.path.join(os.environ['PREFIX'], 'conda-otb/lib/otb/applications')
os.environ['GDAL_DATA'] =  os.path.join(os.environ['PREFIX'], 'share/gdal')
os.environ['PROJ_LIB'] = os.path.join(os.environ['PREFIX'], 'share/proj')
os.environ['GPT_BIN'] = os.path.join(os.environ['PREFIX'], 'snap/bin/gpt')
import otbApplication
import gdal
from shapely.wkt import loads
from shapely.geometry import box, shape, mapping
from shapely.errors import ReadingError
import shutil
from datetime import datetime
import xml.etree.ElementTree as ET
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

3. **Using the target site data access and following the documentation available at the target site**, get two relevant Sentinel-2 MSI L2A products. For instance, the products with the identifier `S2A_MSIL2A_20201026T112151_N0214_R037_T29TPE_20201027T144218` and `S2B_MSIL2A_20201130T112429_N0214_R037_T29TPE_20201130T131854` of November 2020 (Portugal) [30%]

* For **Sobloo**, the download can be operformed using the DirectData API. This is done automatically by the Jupyter notebook, so you can skip this manual step.
* For **Onda**, ... TBD.
* For the other DIASes, ... TBD.


Make sure the contents of the zipped archive are extracted and available and located in a directory accessible by Jupyter Notebook (adjust the notebook cell under *Data location* as required). [40%]

```console
$ unzip S2A_MSIL2A_20201026T112151_N0214_R037_T29TPE_20201027T144218.zip
$ unzip S2B_MSIL2A_20201130T112429_N0214_R037_T29TPE_20201130T131854.zip
```
4. Return to Jupyter Notebook, open the notebook with a Python 3.5 kernel. If you created and new conda environment during the installation procedure, make sure the Python kernel is using that environment. [50%]

5. Make the appropriate settings in the first cell under *Settings* (self-explaining). Then execute, one after another, the cells of the notebook, waiting for each cell to complete, ensuring no errors occur.

   If the data files were downloaded manually (step 3), you can skip the cells for the data download (under *Data Download*). Otherwise you have to execute the appropriate cell. In this case the download time, which is one of the metrics to record, is measured automatically and reported in the output.
   
   The total execution time of all cells should be somewhere around 20 minutes. [60%]

6. In the directory of your notebook there should now be two GeoTIFF file whose names start with `burned_area...` [90%]

```console
$ ls -l /workspace/TBD
-rw-r--r-- 1 user cdab  13536486 Dec  4 23:13 /workspace/burned_area_20201130_112429_20201130_112429.rgb.tif
-rw-r--r-- 1 user cdab 161748359 Dec  4 23:08 /workspace/burned_area_20201130_112429_20201130_112429.tif
```

8. Download them to your computer and open it with any tool that can visualise TIFF files. Verify that the band/layer `TBD` show an image of the detected fires as in the picture below: [100%]



## Application build procedure 

There is no systematically run application for this scenario, so no application and container need to be built.