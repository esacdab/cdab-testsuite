# Use Case Scenario #1 - NDVI mapping

=======
## Story

The use case describes a very basic story where the main user wants to compute vegetation maps  through a typical workflow for calculating the normalized difference vegetation index (NDVI) using Sentinel-2 data with a simple python program. NDVI provides a measure of healthy vegetation and ranges in value from -1 to 1. Values closer to 1 represent healthy, green vegetation. NDVI can be calculated from Sentinel-2 data using band 4 (red) and band 8 (near-infrared). Our user wants to execute it NDVI algorithm on-demand from a Sentinel-2 scene selected from a map to compute value-added products relevant to vegetation monitoring applications e.g. agriculture and forestry. The related scenario involves very simple processing or manipulation of same-product parts implying e.g. extraction of features from a given product and computation of a generic index (e.g. bands manipulation). The dataset of relevance for this scenario is limited to a few tiles from Sentinel-2 L1C data. The geographic location is variable over continental land (e.g. XXXXXXXX). The time span covers about XX days and is limited to fresh data (i.e. no off-line data is involved). Emphasis is on the capability to support agile and continuous computation, with average timelines.

## User Profile 

The user is an environment scientist with a basic computer science training. He can program in python using some earth observation data manipulation libraries such as GDAL and numpy. He is not familiar with any other language or dedicated tool.

## Question & Context

> How suitable is the platform for our user wishing to generate NDVI maps from Sentinel-2 scene ? 

Using the scenario template, here are the defined variables used in the various procedure through the scenario

| Variable                                       | Value                                                                                                                                                                                   | Comment                                                                                                    | Used in                 |
| ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- | ----------------------- |
| Payment methods                                | #1 paypal, #2 credit card                                                                                                                                                               | user wants to use exclusively it’s credit card to pay for it’s account                                     | Step #1                 |
| User’s programming language and Tools ability  | python (0.8), gdal (0.2)                                                                                                                                                                | Limited computer skills but very sufficient for the use case                                               | Step #1                 |
| User’s profile description                     | An environmental scientist that would like to integrate an NDVI program in python using some earth observation data, typically Sentinel-2. I need to use software like GDAL and python. | Description to be used in the exchange for describing the user                                             | Step #1                 |
| Development Environment installation procedure | See integration.md in the test suite software package in the corresponding scenario folder                                                                                              | Simple installation steps to have GDAL and python                                                          | Step #2                 |
| Integration script                             | See integration.md in the test suite software package in the corresponding scenario folder                                                                                              | Some integration steps to reproduce                                                                        | Step #2                 |
| Processing Container                           | See Dockerfile in the test suite software package in the corresponding scenario folder                                                                                                  | recipe to build the user application                                                                       | Step #2                 |
| Use Case Data Collection                       | Sentinel-2 L1C                                                                                                                                                                          | Sentinel 2 Level 1 to process the NDVI                                                                     | Step #1,Step #2,Step #3 |
| Processing capacity requirements               | 1 execution on demand at a time                                                                                                                                                         | Very basic requirement for the processing                                                                  | Step #4                 |
| Data visualization capacity                    | WMS (0.3) geobrowser (0.3)                                                                                                                                                              | Having the possibility to visualize the results on map directly without downloading the product is a bonus | Step #5                 |









