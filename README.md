[![Build Status](https://build.terradue.com/job/Terradue/job/cdab-testsuite/job/develop/badge/icon)](https://build.terradue.com/blue/organizations/jenkins/Terradue%2Fcdab-testsuite/activity?branch=develop)

# CDAB Test Suite

Copernicus Sentinels Data Access Worldwide Benchmark Test Suite is the software suite used to run Test Scenarios

=======
## Docker

This repository contains **Dockerfile** of the Test Suite for benchmarking the Copernicus Data Access Services. See the supported target site in the section SupportedTargetSite


### Base Docker Image

* [docker.terradue.com/centos7-testsite](https://docker.terradue.com/artifactory/webapp/#/artifacts/browse/tree/General/dockerv2-local/centos7-testsite/latest)


### Installation

1. Install [Docker](https://www.docker.com/).

2. Download [automated build](https://docker.terradue.com/centos7-testsite) from private [Terradue Docker Hub Registry](https://docker.terradue.com/): 

    docker pull docker.terradue.com/centos7-testsite

(alternatively, you can build an image from the included Dockerfile: 
   
    docker build -t="<your docker hub>/centos7-testsite"

<a name="SupportedTargetSite"></a>
## Supported target sites

The current supported target sites are

* Conventional Data Access Hubs:
  * [Copernicus Open Access Hub (aka SciHub)](https://scihub.copernicus.eu/)
  * [Copernicus Open Access Hub API (aka APIHub)](https://scihub.copernicus.eu/twiki/do/view/SciHubWebPortal/APIHubDescription)
  * [Copernicus Collaborative Data Hub (aka ColHub)](https://colhub.copernicus.eu/)
  * [Copernicus Sentinels International Access Hub (aka IntHub)](https://inthub.copernicus.eu/)
* DIAS
  * [CREODIAS](https://creodias.eu/)
  * [Mundi Web Services](https://mundiwebservices.com/)
  * [ONDA](https://www.onda-dias.eu/)
  * [Sobloo](https://sobloo.eu/)


## General Usage

### General Command Line Access

    docker run -it docker.terradue.com/centos7-testsite /bin/bash

## Executing the Copernicus Data Access Benchmarking Tool and running Test Scenarios

The detailed information for executing the test scenarios are described in [the CDAB Test Client page](doc/CDABClient.md)

