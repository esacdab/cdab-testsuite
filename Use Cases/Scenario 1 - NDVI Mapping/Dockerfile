FROM ubuntu:bionic
MAINTAINER "Emmanuel Mathot" <emmanuel.mathot@terradue.com>

RUN apt-get update

# Install prerequisite
RUN apt-get install -y python3 gdal-bin python3-gdal unzip
# RUN pip install numpy

COPY ndvi.py ndvi.py
RUN chmod +x ndvi.py

