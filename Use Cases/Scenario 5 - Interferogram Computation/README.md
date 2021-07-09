# Use Case Scenario #5 - Interferogram Computation

## Story

The use case describes an advanced story where the main user processes Sentinel-1 SLC data to generate an interferogram over an area of interest. He uses the Sentinel Application Platform (SNAP) to generate the interferogram and does the interferogram phase unwrapping relying on a CLI utility called snaphu. 

The main user selects Sentinel-1 Single Look Complex acquisitions over an area of interest. After having selected the reference acquisition (previously referred to as master), he uses the catalog discovery mechanism to select the other acquisition (previously referred to as slave) setting the same track (orbit) same orbit direction and an overlap of at least 80% between the two acquisitions.

The main user creates a bash script that creates a directed acyclic graph (DAG) for SNAP and does a system call to the SNAP gpt utility to process it. The DAG includes all the steps for the unwrapped interferogram generation. 


## User Profile 

The user is an environmental scientist with intermediate computer science training. He can program in bash and has an advanced knowledge of the SNAP toolbox.

## Question & Context

> How suitable is the platform for our user wishing to generate interferograms from Sentinel-1 products? 

Using the scenario template, here are the defined variables used in the various procedure through the scenario:

| Variable                                       | Value                                                                                                                                                                                   | Comment                                                                                                    | Used in                 |
| ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- | ----------------------- |
| Payment methods                                | #1 Bank transfer, #2 Credit card                                                                                                                                                 | User wants to use exclusively their credit card to pay for their account.                                     | Step #1                 |
| User’s programming language and tools ability  | Java (0.3), SNAP (0.5), snaphu (0.2)                                                                                                                                                                | Advanced computer skills especially with SNAP toolbox and data manipulation with snaphu.                                               | Step #1                 |
| User’s profile description                     | The user is an environmental scientist with intermediate computer science training.  He can program in bash and has an advanced knowledge of the SNAP toolbox. | Description to be used in the exchange for describing the user.                                             | Step #1                 |
| Development Environment installation procedure | See [integration.md](integration.md) in the test suite software package in the corresponding scenario folder (Scenario Repository).                                                                                              | Installation steps to have a working jupyter notebook in python with the snap libraries.                                                          | Step #2                 |
| Integration procedure                          | See [integration.md](integration.md) in the test suite software package in the corresponding scenario folder (Scenario Repository).                                                                                              | Some integration steps to reproduce.                                                                        | Step #2                 |
| Application build procedure                    | See [integration.md](integration.md) in the test suite software package in the corresponding scenario folder (Scenario Repository).                                                                                                  | Recipe to build the user application.                                                                       | Step #2                 |
| Use case data collection                       | Sentinel-1 SLC                  | Sentinel-1 SLC acquisitions pre and post event.                                                                   | Step #1, Step #2, Step #3 |
| Useful data access filter               | Mission, product type or level or collection, geographical AOI, sensing time span, track, land coverage.                                                                                             | Filters to search for Sentinel-1 SLC pairs on the same track for a given AOI.                                                                  | Step #3                 |
| Processing scenario                    | Interferogram Computation                                              | Processing Scenario to execute. | Step #4 |
| Data visualization tools                    | WMS, geobrowser                                                   | Having the possibility to visualize the results on map directly without downloading the product is a bonus. | Step #5                 |
