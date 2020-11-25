# Use Case Scenario #2 - Rapid Mapping

## Story

This scenario concerns the evaluation of the suitability of the target site for a user wishing to compute value-added products relevant to create burned area analysis map and NIR/SVWI RGB composites after a wildfire event using Sentinel-2 MSI L2A products.

The scenario involves the creation of a directed acyclic graph (DAG) for SNAP and to call the SNAP gpt utility to calculate the Normalized Burn Ratio (dNBR) and the Relativized Burn Ratio (RBR) between pre-fire and post-fire acquisitions from within a Jupyter Notebook.
Focus is on stringent timeliness and the capability through catalogue search of identification of best pre- and post- event images.

The user wishes to calculate the Normalized Burn Ratio (dNBR) and the Relativized Burn Ratio (RBR) between pre-fire and post-fire acquisitions. The Burned Area map is obtained through the intersection between AOI and the pre-fire, post-fire Sentinel-2 chosen as input products.

The dataset of relevance for this scenario is limited to Sentinel-2 MSI L2A products over a continental land (e.g. Australia). The time span covers a few days and is limited to fresh data (i.e. no off-line data is involved). Timeliness of availability of latest Sentinel-2 acquisitions is very important. Filters to search for Sentinel-2 MSI L2A over a specific AOI/geoname for a given timespan over land area with the post-fire image very close in time to the fire date is important.

## User Profile 

The user is an environmental engineer with intermediate computer science training working for an organization involved in Disasters Risk Management. He can program in Python and has an intermediate knowledge of the SNAP toolbox. He is looking for an interactive development environment allowing to rapidly prototype the service with intermediate checkpoints.

## Question & Context

> How suitable is the platform for our user wishing to rapidly generate maps maps from a Sentinel-2 data product? 

Using the scenario template, here are the defined variables used in the various procedure through the scenario

| Variable                                       | Value                                                                                                                                                                                   | Comment                                                                                                    | Used in                 |
| ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- | ----------------------- |
| Payment methods                                | #1 PayPal, #2 Credit card                                                                                                                                                 | User wants to use exclusively their credit card to pay for their account.                                     | Step #1                 |
| User’s programming language and tools ability  | Python (0.3), SNAP (0.3), OTB (0.1), Jupyter notebook (0.3)                                                                                                                                                                | Good computer skills and willing to use an interactive tool to preview integration results.                                               | Step #1                 |
| User’s profile description                     | The user is an environmental engineer with intermediate computer science training working for an organization involved in Disasters Risk Management. He can program in Python and has an intermediate knowledge of the SNAP toolbox. He is looking for an interactive development environment allowing to rapidly prototype the service with intermediate checkpoints. | Description to be used in the exchange for describing the user.                                            | Step #1                 |
| Development Environment installation procedure | See integration.md in the test suite software package in the corresponding scenario folder (Scenario Repository).                                                                                              | Installation steps to have a working Jupyter notebook in Python with the SNAP libraries.                                                          | Step #2                 |
| Integration script                             | See integration.md in the test suite software package in the corresponding scenario folder (Scenario Repository).                                                                                              | Some integration steps to reproduce.                                                                        | Step #2                 |
| Application build procedure                    | See integration.md in the test suite software package in the corresponding scenario folder (Scenario Repository).                                                                                                  | Recipe to build the user application.                                                                       | Step #2                 |
| Use case data collection                       | Sentinel-2 MSI L2A                                                                                                                                                                           | Sentinel-2 MSI L2A pre- and post-event acquisitions to map the burnt area.                                                                     | Step #1, Step #2, Step #3 |
| Useful data access filter               | Mission, product type or level or collection, geographical AOI, sensing time span, cloud coverage, land coverage, update datetime.                                                                                             | Filters to search for Sentinel-2MSI  Level-2A acquisitions over a specific AOI in a recent timespan and as close as possible in time, excluding too high cloud coverage.                                                                  | Step #3                 |
| Processing scenario                    | Rapid Mapping                                                                       | Processing Scenario to execute. | Step #4 |
| Data visualization tools                    | WMS, geobrowser                                                   | Having the possibility to visualize the results on map directly without downloading the product is a bonus. | Step #5                 |
