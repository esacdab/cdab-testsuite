$graph:
  - baseCommand: /ndvi.py
    class: CommandLineTool
    hints:
      DockerRequirement:
        dockerPull: 'docker.terradue.com/cdab-ndvi:latest'
    id: ndvi
    inputs:
      inp1:
        inputBinding:
          position: 1
        type: Directory
    outputs:
      results:
        outputBinding:
          glob: "*_NDVI.tif"
        type: File
    requirements:
      ResourceRequirement: {}
  - class: Workflow
    doc: This service takes as input a Sentinel-2 Level-1C product and produces a NDVI GeoTiff product
    id: wf
    inputs:
      s2_img_data_folder:
        doc: This service takes as input a Sentinel-2 Level-1C product and produces a NDVI GeoTiff product
        label: Sentinel-2 L1C IMG_DATA folder
        stac:collection: s2_img_data_folder
        type: Directory
    label: Sentinel-2 L1C NDVI processing
    outputs:
      - id: wf_outputs
        outputSource:
          - ndvi/results
        type: File
    requirements:
      - class: ScatterFeatureRequirement
    steps:
      ndvi:
        in:
          inp1: s2_img_data_folder
        out:
          - results
        run: '#ndvi'
$namespaces:
  stac: 'http://www.me.net/stac/cwl/extension'
cwlVersion: v1.0
