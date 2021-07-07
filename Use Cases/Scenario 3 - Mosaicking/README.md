# Use Case Scenario #3 - Mosaicking/Land Monitoring

## Story

This scenario concerns the computation of a generic raster derived from mosaicking Sentinel-3 L2 OLCI data of the same type over a regional size area (e.g. Western Africa) and within a given time window (e.g. 10 days composites). The purpose of this scenario is to evaluate the capability for the target site to handle a large number of datasets in a single processing to produce multi temporal products.

Focus is on timeliness and mosaicking of the recently acquired data but also on the processing performances when large quantities of data are involved. The other important aspect is the capacity for the target site to provide with ready to use time-driven processing scheduling/orchestration tools allowing to process systematically the mosaic every N days (where N is configurable).

## User Profile 

The user is a specialist in Earth system science, using satellite remote sensing techniques, responsible for scientific research projects in connection with satellite missions, with a scientific expertise in land, hydrology and forestry. The user and his team are often on the field for in-situ campaigns using satellite remote sensing data. They developed a quick and relatively simple program (black box) to mosaic OLCI data to produce products allowing a global monitoring of the land evolution (e.g. vegetation, desert extent, inland water bodies).

## Question & Context

Using the scenario template, here are the defined variables used in the various procedure through the scenario:

> How suitable is the platform for a user wishing to mosaicking Sentinel-3 OLCI data for land monitoring? 

| Variable                                       | Value                                                                                                                                                                                   | Comment                                                                                                    | Used in                 |
| ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- | ----------------------- |
|  Payment methods                                 | #1 Credit card, #1 Bank transfer |  User wants to use exclusively their credit card to pay for their account. Bank transfer is acceptable but not preferred. | Step #1                 |
|  User’s programming language and tools ability   | Python (0.35), SNAP (0.35), GDAL (0.3) |  Intermediate computer skills especially with SNAP toolbox. | Step #1                 |
|  User’s profile description                      | The user is a specialist in Earth system science, using satellite remote sensing techniques, responsible for scientific research projects in connection with satellite missions, with a scientific expertise in land, hydrology, and forestry. His team can program in python and have an intermediate knowledge of the SNAP toolbox and gdal. They want to easily upload and integrate a script developed at their premises. |  Description to be used in the exchange for describing the user.  | Step #1                 |
|  Development Environment installation procedure  | See [integration.md](integration.md) in the test suite software package in the corresponding scenario folder (Scenario Repository). |  Installation steps to have a working jupyter notebook in python with the snap libraries.                                                          | Step #2                 |
|  Integration procedure                           | See [integration.md](integration.md) in the test suite software package in the corresponding scenario folder (Scenario Repository). |  Some integration steps to reproduce.                                                                        | Step #2                 |
|  Application build procedure                     | See [integration.md](integration.md) in the test suite software package in the corresponding scenario folder (Scenario Repository). |  Recipe to build the user application.                                                                       | Step #2                 |
|  Use case data collection                        | Sentinel-3 OLCI Level 2 |  Sentinel-1 SLC acquisitions pre and post event.                                                                   | Step #1, Step #2, Step #3 |
|  Useful data access filter                | Mission, product type and level, geographical AOI, sensing time span, track. |  Filters to search for Sentinel-1 SLC pairs on the same track for a given AOI.                                                                  | Step #3                 |
|  Processing scenario                     | 10 days mosaic Level 2 composite (NDVI) products |  Processing Scenario to execute. | Step #4 |
|  Exploitation tools                     | * Access to a OGC WMS (e.g. GeoServer, MapServer etc.) where to publish the generated results for visualization and sharing (0.3) * Access to GIS tools to visualize and manipulate results (e.g. QGIS, ArcGIS etc.) (0.4)
    * Access to private storage area for saving (and sharing) the generated results (e.g. S3 bucket, HTTP file server, NextCloud, OwnCloud etc.) (0.2)
    * Access (via API) to a catalogue index allowing publication of basic metadata about the generated results and allow later discovery and sharing (e.g. a OGC OpenSearch Catalogue) (0.1) |  Having the possibility to visualize the results on map directly without downloading the product is a bonus. | Step #5                 |
|  Orchestration / scheduling tools        |      * no scheduler at all
    * crontab on a virtual machine
    * dedicated scheduler triggering containers
    * scheduler solution on infra (e.g. k8s cron jobs)
    * fully integrated scheduler with dashboar
    * DAG orchestrator with scheduler and dashboard |  An important aspect is the capacity for the target site to provide with a time-driven processing scheduling mechanism allowing to re-process systematically the mosaic every 10 days | Step #5                 |



