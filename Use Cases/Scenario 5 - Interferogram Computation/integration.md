# Use Case Scenario #1 - NDVI mapping - Integration Procedures

## Development Environment installation procedure

1. Provision a virtual machine on the target site. Preferably with the following specification:
    - 4 CPU, 32GB RAM, 100GB disk
    - CentOS 7
    - With data offer access if required
  
2. Open a terminal on the provisioned machine.

3. Install some prequisites, in case they are not yet present on the machine.

   ```
   sudo yum install -y vim tree wget unzip libgfortran-4.8.5-39.el7.x86_64
   ```

4. It is assumed that **conda** is not available on the virtual machine. Conda is needed as the vehicle to install the SNAP toolbox.
   Install it via the script below and do point 3 again, in order to verify:

```bash
    CONDA_DIR=/opt/anaconda
    cd $(dirname $0)
    MINIFORGE_VERSION=4.8.2-1

    # SHA256 for installers can be obtained from https://github.com/conda-forge/miniforge/releases
    SHA256SUM="4f897e503bd0edfb277524ca5b6a5b14ad818b3198c2f07a36858b7d88c928db"
    URL="https://github.com/conda-forge/miniforge/releases/download/${MINIFORGE_VERSION}/Miniforge3-${MINIFORGE_VERSION}-Linux-x86_64.sh"
    INSTALLER_PATH=/tmp/miniforge-installer.sh

    # Make sure user's $HOME is not tampered with since this is run as root
    unset HOME
    wget --quiet $URL -O ${INSTALLER_PATH}
    chmod +x ${INSTALLER_PATH}
    # Check sha256 checksum
    if ! echo "${SHA256SUM}  ${INSTALLER_PATH}" | sha256sum  --quiet -c -
    then
        echo "sha256 mismatch for ${INSTALLER_PATH}, exiting!"
        exit 1
    fi
    bash ${INSTALLER_PATH} -b -p ${CONDA_DIR}
    export PATH="${CONDA_DIR}/bin:$PATH"

    # Preserve behavior of miniconda - packages come from conda-forge + defaults
    conda config --system --append channels defaults
    conda config --system --append channels https://conda.binstar.org/terradue
    conda config --system --append channels https://conda.binstar.org/eoepca
    conda config --system --append channels https://conda.binstar.org/r

    # Do not attempt to auto update conda or dependencies
    conda config --system --set auto_update_conda false
    conda config --system --set show_channel_urls true

    # Bug in conda 4.3.>15 prevents --set update_dependencies
    echo 'update_dependencies: false' >> ${CONDA_DIR}/.condarc

    # Avoid future changes to default channel_priority behavior
    conda config --system --set channel_priority "flexible"
```

Check that coda is now available:
```console
$ which conda
/opt/anaconda/bin/conda
$ conda --version
conda 4.8.2
```

5. Install the SNAP toolbox in a new conda environment.

```bash
conda create -n env_snap -y snap
export PATH="${CONDA_DIR}/envs/env_snap/snap/bin:${CONDA_DIR}/envs/env_snap/snap/jre/bin:$PATH"
```


## Integration procedure 

1. Open a terminal on the previously set-up virtual machine. [5%]

2. Upload the current use case folder to the user folder using either SCP or the provider upload tool. 
   Create the folders *input_data* and *output_data* in that location [10%]

3. **Using the target site catalogue access and following the documentation available at the target site**, get a relevant Sentinel-1 SLC product.
   For this guide, we use the example of an earthquake near San Pedro, Philippines.
   The location is a point with the coordinates (in WKT notation) `POINT(124.127 12.026)` and the date `2020-08-18T00:03:48Z`. 

   Search for a product, possibly the first, from the 10-day perriod starting at the moment of the event (earthquake in this example).

   The resulting product will be the **post-event** product. Ideally it should be the one with the identifier **S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739**.
  
   Obtain that product's metadata and extract its download location. [20%]

4. **Using the target site data access and following the documentation available at the target site**, download the product to the *input_data* folder. This usually requires credentials.
   In the case of the above product, the command for download from the Terradue storage would be the following:

    ```console
    $ curl -L -o input_data/S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739.zip https://store.terradue.com/download/sentinel1/files/v1/S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739
    ```

    Make the necessary commands to extract the product as a folder. The product directory should be in the same place as the *.zip* file, which can be deleted after extraction.

    ```console
    $ unzip S2A_MSIL1C_20200507T104031_N0209_R008_T31TFK_20200507T124549.zip
    ```

    Verify that the directory structure and content of the extracted product is as follows:

    ```console
    ├── S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739
    └── download
        └── S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739.SAFE
            ├── annotation
            │   ├── calibration
            │   │   ├── calibration-s1a-iw1-slc-vv-20200815t095747-20200815t095815-033916-03ef29-001.xml
            │   │   ├── calibration-s1a-iw2-slc-vv-20200815t095748-20200815t095813-033916-03ef29-002.xml
            │   │   ├── calibration-s1a-iw3-slc-vv-20200815t095749-20200815t095814-033916-03ef29-003.xml
            │   │   ├── noise-s1a-iw1-slc-vv-20200815t095747-20200815t095815-033916-03ef29-001.xml
            │   │   ├── noise-s1a-iw2-slc-vv-20200815t095748-20200815t095813-033916-03ef29-002.xml
            │   │   └── noise-s1a-iw3-slc-vv-20200815t095749-20200815t095814-033916-03ef29-003.xml
            │   ├── s1a-iw1-slc-vv-20200815t095747-20200815t095815-033916-03ef29-001.xml
            │   ├── s1a-iw2-slc-vv-20200815t095748-20200815t095813-033916-03ef29-002.xml
            │   └── s1a-iw3-slc-vv-20200815t095749-20200815t095814-033916-03ef29-003.xml
            ├── manifest.safe
            ├── measurement
            │   ├── s1a-iw1-slc-vv-20200815t095747-20200815t095815-033916-03ef29-001.tiff
            │   ├── s1a-iw2-slc-vv-20200815t095748-20200815t095813-033916-03ef29-002.tiff
            │   └── s1a-iw3-slc-vv-20200815t095749-20200815t095814-033916-03ef29-003.tiff
            ├── preview
            │   ├── icons
            │   │   └── logo.png
            │   ├── map-overlay.kml
            │   ├── product-preview.html
            │   ├── quick-look.png
            │   └── thumbnail.png
            ├── S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739.SAFE-report-20200815T130109.pdf
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

5. From the post-event's product's metadata obtained in step 3, extract the relative orbit number (track).
   In the case of the above product, it is **69**.

   As in step 3, do a new search for the **pre-event** product, this time for a 10-day from the 10-day perrod ending at the moment of the event. Use the relative orbit number as a query parameter to make sure that the product is from the same track.

   Download and extract the product as in step 4. [35%]


6. Run the graph processor gpt with the arguments explained below.

  The first argument is the graph definition file as from the use case folder.
  The second argument (**pre_event**) has as value the location of the *.SAFE* folder of the unzipped pre-event product.
  Likewise, the third argument (**post_event**) has as value the location of the *.SAFE* folder of the unzipped post-event product.

  The command could look similar to this if the above pre- and post-event files are used:

  ```console
  $ gpt insar.xml \
  -Ppre_event=input_data/S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739/download/S1A_IW_SLC__1SSV_20200815T095747_20200815T095815_033916_03EF29_E739.SAFE \
  -Ppost_event=input_data/S1B_IW_SLC__1SDV_20200821T095714_20200821T095741_023020_02BB48_C5DD/download/S1B_IW_SLC__1SDV_20200821T095714_20200821T095741_023020_02BB48_C5DD.SAFE
  ```

  Make sure the processing starts correctly and does not produce an error within the first minute. [40%]

7. Wait for the processing to complete without error.
   Check the output directory structure and content (under *output_data/target.data*), it should look like this:

  ```console
  ├── coh_VV_15Aug2020_21Aug2020.hdr
  ├── coh_VV_15Aug2020_21Aug2020.img
  ├── i_ifg_VV_15Aug2020_21Aug2020.hdr
  ├── i_ifg_VV_15Aug2020_21Aug2020.img
  ├── q_ifg_VV_15Aug2020_21Aug2020.hdr
  ├── q_ifg_VV_15Aug2020_21Aug2020.img
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

8. Download it to your computer and open it with QGIS [75%]

![NDVI in QIS](T31TFK_20200507T104031_NDVI.png "NDVI in QGIS")


9. Do another search similar to the one in step 5, but over a 3-year period before the pre-event product (i.e. the second product we chose), also the relative orbit number in the search.
   Download the products as before. Make sure all products are downloaded correctly, belong to the correct track and cover the point of interest.

## Application build procedure 

1. Open a terminal on the previously set-up virtual machine.

2. Go to the Use case folder.

3. Launch docker build prefixing the name with the target site docker hub repository (here docker.terradue.com).

  ```console
  $ docker build -t docker.terradue.com/cdab-interferogram .
  ```

4. Push the docker to the hub.

  ```console
  $ docker push docker.terradue.com/cdab-interferogram
  ```