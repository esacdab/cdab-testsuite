# DO NOT USE EURO SIGN or python parsers will fail
service_providers:
  # Service Provider Name (used in the CLI target_name argument)
  cdse:
    max_catalogue_thread: 5
    max_download_size: 1395864371
    data:
      url: https://catalogue.dataspace.copernicus.eu/odata/v1/Products
      credentials: username:password
data:
  sets:
    Sentinel1-RAW:
      label: "Sentinel-1 RAW"
      parameters:
        missionName:
          value: "Sentinel-1"
          label: "Sentinel-1"
        productType:
          value: "RAW"
          label: "RAW"
    Sentinel1-SLC:
      label: "Sentinel-1 SLC"
      parameters:
        missionName:
          value: "Sentinel-1"
          label: "Sentinel-1"
        productType:
          value: "SLC"
          label: "SLC"
    Sentinel1-GRD:
      label: "Sentinel-1 GRD"
      parameters:
        missionName:
          value: "Sentinel-1"
          label: "Sentinel-1"
        productType:
          value: "GRD"
          label: "GRD"
    Sentinel1-OCN:
      label: "Sentinel-1 OCN"
      parameters:
        missionName:
          value: "Sentinel-1"
          label: "Sentinel-1"
        productType:
          value: "OCN"
          label: "OCN"
    Sentinel2-L1C:
      label: "Sentinel-2 Level-1C"
      parameters:
        missionName:
          value: "Sentinel-2"
          label: "Sentinel-2"
        productType:
          value: "S2MSI1C"
          label: "L1C"
    Sentinel2-L2A:
      label: "Sentinel-2 Level-2A"
      parameters:
        missionName:
          value: "Sentinel-2"
          label: "Sentinel-2"
        productType:
          value: "S2MSI2A"
          label: "L2A"
        # We filter out L2Ap products by ignoring the time range before operational distribution
        sensingStart:
          value: "2018-03-20T00:00:00Z"
          label: ""
    Sentinel3-OL_1_EFR___:
      label: "Sentinel-3 OLCI L1 FR NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "OL_1_EFR___"
          label: "OLCI L1 FR"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-OL_1_ERR___:
      label: "Sentinel-3 OLCI L1 RR NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "OL_1_ERR___"
          label: "OLCI L1 RR"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-OL_2_LRR___:
      label: "Sentinel-3 OLCI L2 Land RR NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "OL_2_LRR___"
          label: "OLCI L2 Land RR"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-OL_2_LFR___:
      label: "Sentinel-3 OLCI L2 Land FR NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "OL_2_LFR___"
          label: "OLCI L2 Land FR"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-SL_1_RBT___:
      label: "Sentinel-3 SLSTR L1 RBT NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "SL_1_RBT___"
          label: "SLSTR L1 RBT"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-SL_2_LST___:
      label: "Sentinel-3 SLSTR L2 Land NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "SL_2_LST___"
          label: "SLSTR L2 Land"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-SR_1_SRA___:
      label: "Sentinel-3 SRAL L1 NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "SR_1_SRA___"
          label: "SRAL L1"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-SR_1_SRA_A_:
      label: "Sentinel-3 SRAL L1 A NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "SR_1_SRA_A_"
          label: "SRAL L1 A"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-SR_1_SRA_BS:
      label: "Sentinel-3 SRAL L1 BS NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "SR_1_SRA_BS"
          label: "SRAL L1 BS"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-SR_2_LAN___:
      label: "Sentinel-3 SRAL L2 Land NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "SR_2_LAN___"
          label: "SRAL L2 Land"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-SY_2_SYN___:
      label: "Sentinel-3 SYN L2 NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "SY_2_SYN___"
          label: "SY L2 SYN"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-SY_2_VGP___:
      label: "Sentinel-3 SYN L2 VGP NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "SY_2_VGP___"
          label: "SY L2 VGP"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-SY_2_VG1___:
      label: "Sentinel-3 SYN L2 VG1 NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "SY_2_VG1___"
          label: "SY L2 VG1"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel3-SY_2_V10___:
      label: "Sentinel-3 SYN L2 V10 NTC"
      parameters:
        missionName:
          value: "Sentinel-3"
          label: "Sentinel-3"
        productType:
          value: "SY_2_V10___"
          label: "SY L2 V10"
        timeliness:
          value: "NTC"
          label: "NTC"
    Sentinel5P-L1B:
      label: "Sentinel-5P L1B"
      parameters:
        missionName:
          value: "Sentinel-5 Precursor"
          label: "Sentinel-5P"
        processingLevel:
          value: "L1B"
          label: "L1B"
    Sentinel5P-L2-Offline:
      label: "Sentinel-5P L2 Offline"
      parameters:
        missionName:
          value: "Sentinel-5 Precursor"
          label: "Sentinel-5P"
        processingLevel:
          value: "L2"
          label: "L2"
        processingMode:
          value: "Offline"
          label: "Offline"
    # S5PHub only keeps L2 NRT products since last 30 days, so we count them separately from Offline products
    # and we limit to last month for comparison with targets which keep them for longer instead.
    Sentinel5P-L2-NRT:
      label: "Sentinel-5P L2 NRT last month"
      parameters:
        missionName:
          value: "Sentinel-5 Precursor"
          label: "Sentinel-5P"
        processingLevel:
          value: "L2"
          label: "L2"
        processingMode:
          value: "Near real time"
          label: "NRT"
        sensingStart:
          value: "[NOW-1M]"
          label: "last 1M"
    Sentinel5P-L2-Reprocessing:
      label: "Sentinel-5P L2 Reprocessing"
      parameters:
        missionName:
          value: "Sentinel-5 Precursor"
          label: "Sentinel-5P"
        processingLevel:
          value: "L2"
          label: "L2"
        processingMode:
          value: "Reprocessing"
          label: "RPRO"
