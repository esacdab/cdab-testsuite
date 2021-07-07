# Use Case Scenario #4 - Trends assessment

## Story

This scenario concerns processing of multiple data of the same type over long-time windows to build land surface temperature time series from Sentinel-3 SLTRS Level 2 data. Concerned time spans and area sizes can range from medium to very large ones.

Focus is on the capacity for the target site to provide with a data-driven processing scheduling mechanism allowing to process systematically new acquired data to produce quickly new complete time series.

Options for storage and products exploitation/visualization (e.g. catalogue, WMS, Time Series viewer) may be at stake.

## User Profile

The user is a scientist working in a Climate Monitoring Institute. He has strong thematic background and intermediate computer science training. He has developed in-house a LST processing service providing land surface temperature time series based on SLSTR instruments data that he wants to deploy on a Cloud environment.

## Question & Context

> How suitable is the platform for a user wishing to build long term land surface temperature (LST) time series from Sentinel-3 SLSTR data? 

| Variable | Value | Comment | Used in |
| -------- |------ | ------- | ------- |
|  Payment methods | #1 Credit card, #1 Bank transfer |  User wants to use exclusively their credit card to pay for their account. Bank transfer is acceptable but not preferred. | Step #1 |
|  User’s programming language and tools ability | Python (0.35), SNAP (0.35), GDAL (0.3) |  Intermediate computer skills especially with SNAP toolbox. | Step #1 |
|  User’s profile description | The user is a scientist working in a Climate Monitoring Institute. He has strong thematic background and intermediate computer science training. He has developed in-house a LST processing service providing land surface temperature time series based on SLSTR instruments data that he wants to deploy on a Cloud environment. He can program in python and has good knowledge of the SNAP toolbox and GDAL. He wants to reuse scripts already developed in-house and adapt them to run on the target site with minimal changes. | User wants as much as possible to reuse previously developed scripts in the target environment. | Step #1 |
|  Development Environment installation procedure | See [integration.md](integration.md) in the test suite software package in the corresponding scenario folder (Scenario Repository). | Installation steps to have a working python env with the _snap_ libraries. | Step #2 |
|  Integration procedure | See [integration.md](integration.md) in the test suite software package in the corresponding scenario folder (Scenario Repository). |  Installation steps to have a working python env with the snap libraries. | Step #2 |
|  Application build procedure | See [integration.md](integration.md) in the test suite software package in the corresponding scenario folder (Scenario Repository). |  Recipe to build the user application. | Step #2 |
|  Use case data collection | Sentinel-3 SLSTR Level 2 |  Sentinel-3 SLSTR L2 acquisitions over AOI and from the start of the mission. | Step #1, Step #2, Step #3 |
|  Useful data access filter | Mission, product type and level, geographical AOI, sensing time span, track. | Filters to search for Sentinel-2 SLSTR Level 2 intersecting the given AOI and on a given track. | Step #3 |
|  Processing scenario | Time series of land surface temperature (LST) products. |  Processing Scenario to execute. | Step #4 |
|  Exploitation tools | <ul><li>Access to a OGC WMS (e.g. GeoServer, MapServer etc.) where to publish the generated results for visualization and sharing (0.3)</li><li>Access to GIS tools to visualize and manipulate results (e.g. QGIS, ArcGIS etc.) (0.4)</li><li>Access to private storage area for saving (and sharing) the generated results (e.g. S3 bucket, HTTP file server, NextCloud, OwnCloud etc.) (0.2)</li><li>Access (via API) to a catalogue index allowing publication of basic metadata about the generated results and allow later discovery and sharing (e.g. a OGC OpenSearch Catalogue) (0.1)</li></ul> |  Having the possibility to visualize the results on map directly without downloading the product is a bonus. | Step #5 |
|  Orchestration / scheduling tools | Data-driven orchestration/scheduling of the processes: <ul><li>No solution</li><li>Polling catalogue from a VM</li><li>Dedicated scheduler triggering containers based on catalogue search</li><li>Dedicated scheduler triggering containers based on catalogue search with dashboard</li><ul> | To process systematically the time series when a new acquisition is available. | Step #5 |
