# Use Case Scenario #5 - Interferogram Computation - Integration Procedures

## Development Environment installation procedure

1.  Provision a virtual machine on the target site. Preferably with the following specification:
    - 4 CPU, 32GB RAM, 100GB disk
    - CentOS 7
    - With data offer access if required
  
2.  Open a terminal on the provisioned machine and install some prequisites, in case they are not yet present on the machine.

    ```
    sudo yum install -y vim tree wget unzip libgfortran-4.8.5
    ```

3.  Install, if necessary, **conda** on the virtual machine and create the conda environment. Conda is needed as the vehicle to install the SNAP toolbox.

    Transfer the included file _conda-install.sh_ on the virtual machine.

    Run the following commands:

    ```console
    sudo sh conda-install.sh
    source /opt/anaconda/etc/profile.d/conda.sh
    ```

4.  Install the SNAP toolbox in a new conda environment.

    Transfer the the included file _environment.yml_ there and create a new conda environment (name **env_snap**) and activate that environment using these commands:
  
    ```console
    sudo chown -R $USER:$USER /opt/anaconda/
    # This avoids permission errors during the conda environment installation

    conda env create --file environment.yml
    # This takes a while. Follow the instructions and confirm.

    conda activate env_snap
    ```

    You may have to log out and log in again for the changes to take effect.


## Integration procedure 

1.  Open a terminal on the previously set-up virtual machine. [5%]

2.  Upload the current use case folder to the user folder using either *scp* or the provider upload tool. 
    Create the folders *input_data* and *output_data* in that location [10%]

3.  **Using the target site catalogue access and following the documentation available at the target site**, get a relevant Sentinel-1 SLC product.
    For this guide, we use the example of an earthquake near San Pedro, Philippines.
    The location is a point with the coordinates (in WKT notation) `POINT(124.127 12.026)` and the date `2020-08-18T00:03:48Z`. 

    Search for a product, possibly the first, from the 10-day perriod starting at the moment of the event (earthquake in this example).

    The resulting product will be the **post-event** product. It should be the one with the identifier **S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7**.
  
    Obtain that product's metadata and extract its download location. [20%]

4.  **Using the target site data access and following the documentation available at the target site**, download the product to the *input_data* folder. This usually requires credentials.

    * For **Sobloo**, the download can be operformed using the DirectData API.

      Create the following script (make sure the API key is set correctly):
      ```python
      import requests
      import re
      import json
      from datetime import datetime
      from pathlib import Path

      sobloo_api_key = ''
      data_path = 'input_data'
      url_regex = re.compile('.*\.SAFE(/(?P<dir>[^\?]*))?(/(?P<file>[^/\?]+))(\?.*)?')

      time_start = datetime.utcnow()
      id = 'S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7'
      response = requests.post("https://sobloo.eu/api/v1-beta/direct-data/product-links",
          headers = {'Authorization': 'Apikey {0}'.format(sobloo_api_key)},
          json={'product': id, "regexp": "(.*)"}
      )
      
      result = json.loads(response.text)
      
      download_list = [ l['url'] for l in result['links'] ]
      for url in download_list:
          m = url_regex.match(url)
          if not m:
              raise('Unrecognised URL pattern: {0}'.format(url))

          file_dir = "{0}/{0}.SAFE{1}{2}".format(id, '/' if m.group('dir') else '', m.group('dir') if m.group('dir') else '')
          file_name = m.group('file')

          print("- Downloading {0}".format(file_name))
          Path("{0}/{1}".format(data_path, file_dir)).mkdir(parents=True, exist_ok=True)

          location = "{0}/{1}/{2}".format(data_path, file_dir, file_name)
          r = requests.get(url, stream=True)
          with open(location, 'wb') as f:
              for chunk in r.iter_content(chunk_size=8192): 
                  f.write(chunk)
          r.close()

      ```

      Run that script with *python3* to download the files.


    * For **ONDA**, do the following:
  
      From the shell, mount the data volume as explained in [this page](https://www.onda-dias.eu/cms/knowledge-base/adapi-how-to-mount-unmount/).
      Copy the .zip files using the following commands:
  
      ```console
      # /local_path is the mountpoint for the data volume
      mkdir input_data/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7
      
      # Locate the file S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.zip
      # in one of the many subdirectories of /local_path/S1
      # and set file=<location>
      cp $file input_data/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7
      ```

    * For **CREODIAS**, make sure your virtual machine has access to the EO Data volume (mounted under `/eodata/`).
      Copy the directories using the following commands:

      ```console
      mkdir input_data/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7
      cp -r /eodata/Sentinel-1/SAR/SLC/2020/08/27/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.SAFE input_data/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7/
      ```

    * For **MUNDI**, do the following:
      Make sure you are in the correct conda environment (*env_snap*) and install the **s3cmd** for S3 access:
      
      ```console
      conda install s3cmd
      ```

      Configure the access to the MUNDI object store. Below there are simplified instructions for this, the original procedure can be found on [this page](https://docs.otc.t-systems.com/en-us/ugs3cmd/obs/en-us_topic_0051060814.html).

      Run
      ```console
      s3cmd --configure
      ```
      You will be prompted for several settings. Enter the following values:

      * Access Key: *enter your MUNDI S3 key ID*
      * Secret Key: *enter your MUNDI S3 secret key*
      * Default Region: **eu-de**
      * S3 Endpoint: **obs.eu-de.otc.t-systems.com**
      * DNS-style bucket+hostname:port template for accessing a bucket: **%(bucket)s.obs.eu-de.otc.t-systems.com**
      * Encryption password: *confirm default*
      * Path to GPG program: *confirm default*
      * Use HTTPS protocol: *confirm default*
      * HTTP Proxy server name: *confirm default*
      
      Answer *n* (no) to an access test and *y* (yes) to saving the settings.

      Edit the file *~/.s3cfg*.

      Locate the line setting the value for `website_endpoint` and change it to:
      
      ```
      website_endpoint = http://%(bucket)s.obs-website.%(location)s.otc.t-systems.com
      ```
      Rerun
      ```console
      s3cmd --configure
      ```
      Confirm all choices and run answer *Y* (yes) to the access test. It should be successful. Answer *N* (no) to saving the settings as they are already fine.

      Now, run the following commands to download the files into the correct location using **s3cmd** (note that not all areas are covered, the file might not be available):

      ```console
      mkdir -p input_data/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7
      s3cmd get s3://s1-l1-slc-2020-q3/2020/08/27/IW/SV/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.zip input_data/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7/
      ```

      
    * For **WEkEO**, do the following:

      The included file _wekeo-tool.py_ is intended to make the download from WEkEO easy. Transfer that file on the virtual machine.

      Set the environment variable `WEKEO_CREDS` with your WEkEO username and password:

      ```console
      WEKEO_CREDS='<username>:<password>'
      ```

      Run the following command:
        
      ```console
      python3 wekeo-tool.py query --credentials="$WEKEO_CREDS" --pn=Sentinel-1 --pt=SLC --uid=S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7 > S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.url
      ```
        
      If the *\*.url* files is empty, the product is not available from WEkEO.
      Download it from another source (see below) or do a different query with a different region and/or period, and retry.

      If the file is available and contains a URL you can download the product with this command:

      ```console
      mkdir input_data/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7
      python3 wekeo-tool.py download --credentials="$WEKEO_CREDS" --url="$(cat S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.url)" --dest="S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.zip"
      unzip -d input_data/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7 S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.zip
      rm S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.zip
      ```

    

    The Terradue storage can be used as an alternative download source in case of unavailability elsewhere. The download command for above product would be the following:

    ```console
    $ curl -L -o input_data/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.zip https://store.terradue.com/download/sentinel1/files/v1/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7
    ```

    Make the necessary commands to extract the product as a folder. The product directory should be in the same place as the *.zip* file, which can be deleted after extraction.

    ```console
    $ unzip S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.zip
    ```

    Verify that the directory structure and content of the extracted product is as follows:

    ```
    S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7
    └── S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.SAFE
        ├── annotation
        │   ├── calibration
        │   │   ├── calibration-s1a-iw1-slc-vv-20200827t095748-20200827t095816-034091-03f552-001.xml
        │   │   ├── calibration-s1a-iw2-slc-vv-20200827t095749-20200827t095814-034091-03f552-002.xml
        │   │   ├── calibration-s1a-iw3-slc-vv-20200827t095750-20200827t095815-034091-03f552-003.xml
        │   │   ├── noise-s1a-iw1-slc-vv-20200827t095748-20200827t095816-034091-03f552-001.xml
        │   │   ├── noise-s1a-iw2-slc-vv-20200827t095749-20200827t095814-034091-03f552-002.xml
        │   │   └── noise-s1a-iw3-slc-vv-20200827t095750-20200827t095815-034091-03f552-003.xml
        │   ├── s1a-iw1-slc-vv-20200827t095748-20200827t095816-034091-03f552-001.xml
        │   ├── s1a-iw2-slc-vv-20200827t095749-20200827t095814-034091-03f552-002.xml
        │   └── s1a-iw3-slc-vv-20200827t095750-20200827t095815-034091-03f552-003.xml
        ├── manifest.safe
        ├── measurement
        │   ├── s1a-iw1-slc-vv-20200827t095748-20200827t095816-034091-03f552-001.tiff
        │   ├── s1a-iw2-slc-vv-20200827t095749-20200827t095814-034091-03f552-002.tiff
        │   └── s1a-iw3-slc-vv-20200827t095750-20200827t095815-034091-03f552-003.tiff
        ├── preview
        │   ├── icons
        │   │   └── logo.png
        │   ├── map-overlay.kml
        │   ├── product-preview.html
        │   └── quick-look.png
        ├── S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.SAFE-report-20200827T124738.pdf
        └── support
            ├── s1-level-1-calibration.xsd
            ├── s1-level-1-measurement.xsd
            ├── s1-level-1-noise.xsd
            ├── s1-level-1-product.xsd
            ├── s1-level-1-quicklook.xsd
            ├── s1-map-overlay.xsd
            ├── s1-object-types.xsd
            └── s1-product-preview.xsd
    ```
    [30%]


5.  From the post-event's product's metadata obtained in step 3, extract the relative orbit number (track).
    In the case of the above product, it is **69**.

    As in step 3, do a new search for the **pre-event** product, this time for a 10-day from the 10-day perrod ending at the moment of the event. Use the relative orbit number as a query parameter to make sure that the product is from the same track.

    A good candidate for the pre-event product is **S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739**.

    Download and extract the product as in step 4. [35%]


6.  Due to the retirement of the legacy server providing Sentinel-1 orbit fiiles (POEORB), the automatic download of those files does not work any more.
    They have therefore to be searched via the new search API at https://scihub.copernicus.eu/gnss/search and downloaded manually from the provided links.
    
    The appropriate files (the orbit files covering both pre- and post-event) have to be added in a structure as shown below in the directory *snap/.snap/auxdata/Orbits/Sentinel-1/POEORB/* under the root directory that contains the conda environment. The full path would usually be */opt/anaconda/envs/env_snap/snap/.snap/auxdata/Orbits/Sentinel-1/POEORB/.  Note that the files have to be zipped.

    ```
    └── S1A
    │   └── 2020
    │       └── 08
    │           ├── S1A_OPER_AUX_POEORB_OPOD_20210317T062946_V20200814T225942_20200816T005942.EOF.zip
    │           └── S1A_OPER_AUX_POEORB_OPOD_20210317T102135_V20200826T225942_20200828T005942.EOF.zip
    ```

    The included script *get-poeorb.py* automates this entire step. It takes the root dir of the conda environment and any number of Sentinel-1 input identifiers (of the pre- and post-event products), as in this example:

    ```bash
    python get-poeorb.py /opt/anaconda/envs/env_snap/ S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739 S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7
    ```

    Make sure that the directory structure and contained files correspond to the above example. [40%]


7.  Run the graph processor **gpt** with the arguments explained below.

    The first argument is the graph definition file that can be uploaded from the use case folder (*insar.xml*).
    The second argument (**pre_event**) has as value the location of the *.SAFE* folder of the unzipped pre-event product.
    Likewise, the third argument (**post_event**) has as value the location of the *.SAFE* folder of the unzipped post-event product.

    The command would look similar to this if the above pre- and post-event files are used:

    ```console
    $ gpt insar.xml \
    -Ppre_event=input_data/S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739/S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739.SAFE \
    -Ppost_event=input_data/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7/S1A_IW_SLC__1SSV_20200827T095748_20200827T095816_034091_03F552_00E7.SAFE
    ```

    Make sure the processing starts correctly and does not produce an error within the first minute. [45%]

8.  Wait for the processing to complete without error.
    Check the output directory structure and content (under *output_data/target.data*), it should look like this:

    ```
    target.data/
    ├── coh_VV_15Aug2020_27Aug2020.hdr
    ├── coh_VV_15Aug2020_27Aug2020.img
    ├── i_ifg_VV_15Aug2020_27Aug2020.hdr
    ├── i_ifg_VV_15Aug2020_27Aug2020.img
    ├── q_ifg_VV_15Aug2020_27Aug2020.hdr
    ├── q_ifg_VV_15Aug2020_27Aug2020.img
    ├── tie_point_grids
    │   ├── incident_angle.hdr
    │   ├── incident_angle.img
    │   ├── latitude.hdr
    │   ├── latitude.img
    │   ├── longitude.hdr
    │   ├── longitude.img
    │   ├── slant_range_time.hdr
    │   └── slant_range_time.img
    └── vector_data
        ├── ground_control_points.csv
        └── pins.csv
    ```

  [60%]

9. Download it to your computer and open it with a suitable tool to verify that the *.img* files show an interferogram. [80%]


10. Do another search similar to the one in step 5, but over a 3-year period before the pre-event product (i.e. the second product we chose), also the relative orbit number in the search.
   Analysing the metadata, make sure the products belong to the correct track and cover the area of interest.
   Download the products as before. For this test. it is not necessary to extract the content. Check the content of the zip files, using this command:
   
   ```console
   unzip --list <file>
   ```
   [100%]



##  Application build procedure 

1.  Open a terminal on the previously set-up virtual machine.

2.  Go to the Use case folder.

3.  Launch docker build prefixing the name with the target site docker hub repository (here docker.terradue.com).

    ```console
    $ docker build -t docker.terradue.com/cdab-interferogram .
    ```

4.  Push the docker to the hub.

    ```console
    $ docker push docker.terradue.com/cdab-interferogram
    ```
