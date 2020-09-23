# Use Case Scenario #2 - Rapid Mapping

## Story

The use case describes an intermediate story where the main user wants to use the Sentinel Application Platform (SNAP) toolbox and Python to detect active fires using Sentinel-3 SLSTR Level-1 day acquisitions.
 
SLSTR stands for Sea and Land Surface Temperature Radiometer. It is a dual scan temperature radiometer in the low Earth orbit (800 - 830 km altitude) on board the Sentinel-3 satellite. It employs along track scanning dual view (nadir and backward oblique) technique for 9 channels in the visible (VIS), thermal (TIR) and short wave (SWIR) infra-red spectrum.

It also provides two dedicated channels for fire and high temperature event monitoring at 1 km resolution (by extending the dynamic range of the 3.74μm channel and including dedicated detectors at 10.85μm that are capable of detecting fires at ~650 K without saturation).

The processing of day and night acquisitions differs slightly and this story is focussed on day acquisitions.

As the SLSTR Level-1 products are provided as radiances these must be converted to reflectances using the SNAP _Radiance-to-Reflectance_ processor. 

Furthermore the brightness temperature (BT) bands of Sentinel-3 RBT products have resolution of 1Km while the radiance (converted to reflectance) bands have resolution of 500m, it is thus necessary to have all bands in the same resolution. The reflectances are thus resampled to 1Km. 

Finally, the Sentinel-3 Level-1B products are geocoded but not projected, therefore the data needs to be reprojected.

## User Profile 

The user is an environmental scientist with intermediate computer science training working for an organization involved in Disasters Risk Management. He can program in Python and has an intermediate knowledge of the SNAP toolbox.

## Question & Context

> How suitable is the platform for our user wishing to rapidly generate maps maps from a Sentinel-3 data product? 

Using the scenario template, here are the defined variables used in the various procedure through the scenario

| Variable                                       | Value                                                                                                                                                                                   | Comment                                                                                                    | Used in                 |
| ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- | ----------------------- |
| Payment methods                                | #1 Bank transfer, #2 Credit card                                                                                                                                                 | User wants to use exclusively their credit card to pay for their account.                                     | Step #1                 |
| User’s programming language and tools ability  | Python (0.4), SNAP (0.4), Jupyter notebook (0.2)                                                                                                                                                                | Good computer skills and willing to use an interactive tool to preview integration results.                                               | Step #1                 |
| User’s profile description                     | The user is an environmental scientist with intermediate computer science training working for an organization involved in Disasters Risk Management. He can program in Python and has an intermediate knowledge of the SNAP toolbox. | Description to be used in the exchange for describing the user.                                             | Step #1                 |
| Development Environment installation procedure | See integration.md in the test suite software package in the corresponding scenario folder (Scenario Repository).                                                                                              | Installation steps to have a working Jupyter notebook in Python with the SNAP libraries.                                                          | Step #2                 |
| Integration script                             | See integration.md in the test suite software package in the corresponding scenario folder (Scenario Repository).                                                                                              | Some integration steps to reproduce.                                                                        | Step #2                 |
| Application build procedure                    | See integration.md in the test suite software package in the corresponding scenario folder (Scenario Repository).                                                                                                  | Recipe to build the user application.                                                                       | Step #2                 |
| Use case data collection                       | Sentinel-3 SLSTR Level-1 Descending                                                                                                                                                                          | Sentinel-3  SLSTR Level-1 Descending day acquisition to detect burnt area.                                                                     | Step #1, Step #2, Step #3 |
| Useful data access filter               | Mission, product type or level or collection, geographical AOI, sensing time span, Cloud Coverage, orbit direction, update datetime.                                                                                             | Filters to search for Sentinel-3  SLSTR Level-1 Descending day acquisition over a specific AOI in a recent timespan excluding too high cloud coverage.                                                                  | Step #3                 |
| Processing scenario                    | Rapid Mapping                                                                       | Processing Scenario to execute. | Step #4 |
| Data visualization tools                    | WMS, geobrowser                                                   | Having the possibility to visualize the results on map directly without downloading the product is a bonus. | Step #5                 |
