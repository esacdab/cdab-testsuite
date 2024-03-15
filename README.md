![CDAB logo](doc/images/cdab-logo.jpg)

# CDAB Software Test Suite

Copernicus Sentinels Data Access Worldwide Benchmark Test Suite is the software suite used to run Test Scenarios for bechmarking various Copernicus Data Provider targets.

The current supported Target Sites are

* [Copernicus Data Space Ecosystem (CDSE)](https://dataspace.copernicus.eu/)
* [CREODIAS](https://creodias.eu/)
* [Mundi Web Services](https://mundiwebservices.com/)
* [ONDA](https://www.onda-dias.eu/)
* [Alaska Satellite Facility](https://www.asf.alaska.edu/)
* [Google Cloud Storage](https://cloud.google.com/storage/docs/public-datasets?)
* [Amazon Web Services](https://registry.opendata.aws/)
* [Hellenic National Sentinel Data Mirror Site](https://sentinels.space.noa.gr/)
* [Wekeo](https://www.wekeo.eu/)
* [Microsoft Planetary Computer](https://planetarycomputer.microsoft.com/)

# Repository Content

This repository is a public repository with all the source code used for building the CDAB Test Suite

The CDAB Test Suite is built automatically providing a docker image available publicly at [ghcr.io/esacdab/cdab-testsuite](https://github.com/esacdab/cdab-testsuite/pkgs/container/cdab-testsuite) that can be used as Test Site.

# Getting Started

You can start now using the Test Suite following the [Getting Started guide](https://github.com/Terradue/cdab-testsuite/wiki)

# Software licenses

The CDAB Test Suite is released as open source software under the GNU Affero General Public License (AGPLv3). This repository contains the source code of an executable DotNet solution combining the unmodified exact copies of the following software packages as dynamic libraries configured as dependencies:

| Software package | License type | Link to license |
| --- | --- | --- |
| log4net | Apache 2.0 | https://logging.apache.org/log4net/license.html |
| Newtonsoft | MIT | https://github.com/JamesNK/Newtonsoft.Json/blob/master/LICENSE.md |
| Mono.Options | MIT | https://github.com/mono/mono/blob/master/mcs/class/Mono.Options/Mono.Options/Options.cs (stated in source code) |
| YamlDotNet | MIT | https://github.com/aaubry/YamlDotNet/blob/master/LICENSE.txt |
| Terradue.OpenSearch | AGPL 3.0 | https://github.com/Terradue/DotNetOpenSearch/blob/master/LICENSE |
| Terradue.OpenSearch.SciHub | CC BY-NC-ND 3.0 | https://creativecommons.org/licenses/by-nc-nd/3.0/ |
| Terradue.GeoJson | AGPL 3.0 | https://github.com/Terradue/DotNetGeoJson/blob/master/LICENSE |
| Terradue.ServiceModel.Syndication | AGPL 3.0 | https://github.com/Terradue/DotNetSyndication/blob/master/LICENSE |
| Terradue.Metadata.EarthObservation | AGPL 3.0 | https://github.com/Terradue/DotNetEarthObservation/blob/master/LICENSE |
| Terradue.ServiceModel.Ogc | AGPL 3.0 | https://github.com/Terradue/DotNetOgcModel/blob/master/LICENSE |

The CDAB Test Suite is free software: it can be redistributed and/or modified under the terms of the GNU Affero General Public License (AGPLv3) as published by the Free Software Foundation, either version 3 of the License, or any later version.

<hr/>
<p align="center">Funded by EU</p>
<p align="center"><img src="doc/images/copernicus-logo.png" alt="Copernicus" height="125"/><img src="doc/images/esa-logo.png" alt="ESA" height="125"/></p>
