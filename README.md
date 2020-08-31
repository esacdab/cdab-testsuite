[![Build Status](https://build.terradue.com/job/Terradue/job/cdab-testsuite/job/master/badge/icon)](https://build.terradue.com/blue/organizations/jenkins/Terradue%2Fcdab-testsuite/activity?branch=master)

![CDAB logo](doc/images/cdab-logo.jpg)

# CDAB Software Test Suite

Copernicus Sentinels Data Access Worldwide Benchmark Test Suite is the software suite used to run Test Scenarios for bechmarking various targets.

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


## Getting started

The CDAB Test Suite is built automatically providing different assets with each release:
- Source code archive
- Set of binaries for the clients as archives and RPM package
- A docker image available publicly as [esacdab/testsuite](https://hub.docker.com/repository/docker/esacdab/testsuite)

## Using the Docker image

### General Command Line Access

    docker run -it esacdab/testsuite /bin/bash

## Executing the Copernicus Data Access Benchmarking Tool and running Test Scenarios

The detailed information for executing the test scenarios are described in [the CDAB Test Suite wiki](https://github.com/Terradue/cdab-testsuite/wiki)
