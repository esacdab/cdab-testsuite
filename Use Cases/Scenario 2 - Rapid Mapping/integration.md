# Use Case Scenario #2 - Rapid mapping - Integration Procedures

## Development Environment installation procedure

1. Provision a virtual machine on the target site. Preferably with the following specification
    - 1 CPU, 8GB RAM, 100GB disk
    - CentOS 7
    - Jupyter Notebook (with Python 3 support)
    - With data offer access if required
  
2. Install, if necessary, conda on the virtual machine and create the conda environment.

   Transfer the included file _conda-install.sh_ on the virtual machine.

   Run the following command:

   ```console
   sudo sh conda-install.sh
   ```

   Transfer the the included file _environment.yml_ there and create a new conda environment (name **env_burned_area**) and activate that environment using these commands:
  
   ```console
   conda env create --file environment.yml
   # This takes a while. Follow the instructions and confirm.

   conda activate env_burned_area
   ```

   You may have to log out and log in again for the changes to take effect.

3. Install and start Jupyter Lab.

   When in the correct environment (shown in the command prompt), you can install Jupyter Lab.

   ```console
   conda install -c conda-forge jupyterlab
   ```

   The following steps are taken from [this page](https://agent-jay.github.io/2018/03/jupyterserver/).

   Execute the commands one after another and follow the instructions:
  
   ```console
   # Create a configuration file template
   jupyter notebook --generate-config
  
   # Set a password for accessing Jupyter and remember it,
   # the password hash is written to a file
   jupyter notebook password
  
   # Create a self-signed certificate for a secure connection
   openssl req -x509 -nodes -days 365 -newkey rsa:2048 -keyout mycert.pem -out mycert.pem
   ```

   Copy the content from the file generated in the password command (usually _~.jupyter/jupyter_notebook_config.json_).

   Edit the configuration file (usually _.jupyter/jupyter_notebook_config.py_).

   Add these lines to the file (setting the appropriate values for _certfile_, _keyfile_ and _password_):
   
   ```
   c.NotebookApp.certfile = u'<path-to-mycert.pem>'
   c.NotebookApp.keyfile = u'<path-to-mycert.pem>'
   c.NotebookApp.ip = '*'
   c.NotebookApp.password = u'<password hash obtained above>'
   c.NotebookApp.open_browser = False
   c.NotebookApp.port = 9999
   ```

   Start Jupyter Lab

   ```console
   jupyter lab
   ```

   There should be log messages displayed confirming that Jupyter Lab is running.

4. Connect to Jupyter Lab.

   Using a new shell, use secure port-forwarding (tunnelling). Replace `user` and `hostname` with the values applying to your virtual machine.
  
   ```console
   ssh -N -f -L 8888:localhost:9999 user@hostname
   ```

   Now you can access Jupyter Lab with your browser at https://localhost:8888/lab. Ignore possible browser warnings. Use the previously set password to log in. 

## Integration procedure 

1. Open Jupyter Notebook on the virtual machine or within the environment provided through the target site's web interface. [10%]

2. Upload the files scenario code files (_burned\_area.ipynb_ and the two helper _*.py_ files) to the workspace folder using the Jupyter upload functionality. [20%]

3. **Using the target site data access and following the documentation available at the target site**, get two relevant Sentinel-2 MSI L2A products. For instance,  the products with the identifier `S2A_MSIL2A_20201026T112151_N0214_R037_T29TPE_20201027T144218` and `S2B_MSIL2A_20201130T112429_N0214_R037_T29TPE_20201130T131854` of November 2020 (Portugal) [30%]

   * For **Sobloo**, the download can be operformed using the DirectData API. This is done automatically by the Jupyter notebook, so you can skip this manual step.

   * For **ONDA**, do the following:
  
     From the shell, mount the data volume as explained in [this page](https://www.onda-dias.eu/cms/knowledge-base/adapi-how-to-mount-unmount/).
     Set the `$DATA_PATH` variable to the directory  for local copies of the products and copy the .zip files using the following commands:
  
     ```console
     # /local_path is the mountpoint for the data volume
     mkdir $DATA_PATH/S2A_MSIL2A_20201026T112151_N0214_R037_T29TPE_20201027T144218
     mkdir $DATA_PATH/S2B_MSIL2A_20201130T112429_N0214_R037_T29TPE_20201130T131854
     cp /local_path/S2/2A/MSI/LEVEL-2A/S2MSI2A/2020/10/26/S2A_MSIL2A_20201026T112151_N0214_R037_T29TPE_20201027T144218.zip $DATA_PATH/S2A_MSIL2A_20201026T112151_N0214_R037_T29TPE_20201027T144218
     cp /local_path/S2/2A/MSI/LEVEL-2A/S2MSI2A/2020/11/30/S2B_MSIL2A_20201130T112429_N0214_R037_T29TPE_20201130T131854.zip $DATA_PATH/S2B_MSIL2A_20201130T112429_N0214_R037_T29TPE_20201130T131854
     ```

   * For **CREODIAS**, make sure your virtual machine has access to the EO Data volume (mounted under `/eodata/`).
     Set the `$DATA_PATH` variable to the directory for local copies of the products and copy the directories using the following commands:

     ```console
     mkdir $DATA_PATH/S2A_MSIL2A_20201026T112151_N0214_R037_T29TPE_20201027T144218
     mkdir $DATA_PATH/S2B_MSIL2A_20201130T112429_N0214_R037_T29TPE_20201130T131854
     cp -r /eodata/Sentinel-2/MSI/L2A/2020/10/26/S2A_MSIL2A_20201026T112151_N0214_R037_T29TPE_20201027T144218.SAFE $DATA_PATH/S2A_MSIL2A_20201026T112151_N0214_R037_T29TPE_20201027T144218/
     cp -r /eodata/Sentinel-2/MSI/L2A/2020/11/30/S2B_MSIL2A_20201130T112429_N0214_R037_T29TPE_20201130T131854.SAFE $DATA_PATH/S2B_MSIL2A_20201130T112429_N0214_R037_T29TPE_20201130T131854/
     ```

   Make sure the contents of the product (if necessary, unzip archive from the correct directory) are available and located in a directory accessible by Jupyter Notebook (adjust the notebook cell under *Data location* as required). [40%]

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

7. Download them to your computer and open it with any tool that can visualise TIFF files. Verify that the band/layer `TBD` show an image of the detected fires as in the picture below: [100%]



## Application build procedure 

There is no systematically run application for this scenario, so no application and container need to be built.