$graph:
- baseCommand: gpt
  hints:
    DockerRequirement:
      dockerPull: snap:0.1
  class: CommandLineTool
  id: clt
  inputs:
    inp1:
      inputBinding:
        position: 1
      type: File
    inp2:
      inputBinding:
        position: 2
        prefix: -Ppre_event=
        separate: false
      type: Directory
    inp3:
      inputBinding:
        position: 2
        prefix: -Ppost_event=
        separate: false
      type: Directory
  outputs:
    results:
      outputBinding:
        glob: .
      type: Directory
  requirements:
    EnvVarRequirement:
      envDef:
        PATH: /opt/anaconda/envs/env_snap/bin/:/opt/anaconda/envs/env_snap/snap/bin/:/opt/anaconda/envs/env_snap/snap/jre/bin:/usr/local/bin:/usr/bin:/usr/local/sbin:/usr/sbin
        PREFIX: /opt/anaconda/envs/env_snap
    ResourceRequirement: {}
  stderr: std.err
  stdout: std.out
- class: Workflow
  doc: SNAP SAR Calibration
  id: main
  inputs:
    snap_graph:
      doc: SNAP Graph
      label: SNAP Graph
      type: File
    pre_event:
      doc: Sentinel-1 SLC product SAFE Directory
      label: Sentinel-1 SLC product SAFE Directory
      type: Directory
    post_event:
      doc: Sentinel-1 SLC product SAFE Directory
      label: Sentinel-1 SLC product SAFE Directory
      type: Directory
  label: SNAP SAR Calibration
  outputs:
  - id: wf_outputs
    outputSource:
    - node_1/results
    type: Directory
  steps:
    node_1:
      in:
        inp1: snap_graph
        inp2: pre_event
        inp3: post_event
      out:
      - results
      run: '#clt'
cwlVersion: v1.0
