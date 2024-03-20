# https://hub.docker.com/_/microsoft-dotnet
FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build
WORKDIR /src

# copy csproj and restore as distinct layers
COPY src/cdab-client/cdab-client.csproj /src/cdab-client/
RUN dotnet restore /src/cdab-client/cdab-client.csproj

# copy everything else and build app
COPY src/cdab-client /src/cdab-client
RUN dotnet publish -c release -o /app -r linux-x64 -f net7.0 --self-contained true /src/cdab-client/cdab-client.csproj
RUN ls -l /app

# final stage/image
FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-bullseye-slim-amd64

LABEL org.opencontainers.image.description DESCRIPTION="CDAB Test Suite" \
      org.opencontainers.image.title="CDAB client and remote client" \
      org.opencontainers.image.authors="CDAB Team" \
      org.opencontainers.image.vendor="CDAB Team" \
      org.opencontainers.image.version="$(date -u)" \
      org.opencontainers.image.url="https://github.com/esacdab/cdab-testsuite/pkgs/container/cdab-testsuite"

RUN apt-get update \
  && apt-get upgrade -y \
  && apt-get install -y python3 python3-pip shunit2 traceroute libssl1.1 libgssapi-krb5-2 sysstat sed ca-certificates jq curl \
  openssh-server ssh sudo cron supervisor libxml2-utils \
  && rm -rf /var/lib/apt/lists/* /tmp/*

# Install OpenStack client, Google Cloud Platform Python API, Amazon AWS EC2 Python API and Microsoft Azure Python API
RUN pip3 install --upgrade --no-cache-dir pip \
  && pip3 install --no-cache-dir pyyaml lxml netifaces python-openstackclient==5.1.0 python-cinderclient openstacksdk==1.0.1 \
  google-api-python-client boto3 setuptools-rust azure-identity azure-mgmt-resource azure-mgmt-authorization \
  azure-mgmt-compute azure-mgmt-network urllib3==1.26.6
  
COPY --from=build /app /usr/lib/cdab-client
COPY src/cdab-client/App_Data /usr/lib/cdab-client/App_Data
COPY --chmod=755 src/cdab-client/cdab-client /usr/bin/cdab-client
COPY src/cdab-remote-client/bin/cdab-remote-client /usr/bin/cdab-remote-client
COPY src/cdab-remote-client/libexec /usr/lib/cdab-remote-client

ENV PATH="/usr/bin:${PATH}"

#### Add jenkins user and SSH connection with server ####
# tell ssh to not use ugly PAM
RUN sed -i 's/UsePAM\syes/UsePAM no/' /etc/ssh/sshd_config
RUN sed -i 's/#PubkeyAuthentication\syes/PubkeyAuthentication yes/' /etc/ssh/sshd_config
RUN sed -i 's/#RSAAuthentication\syes/RSAAuthentication yes/' /etc/ssh/sshd_config
RUN sed -i 's/#   StrictHostKeyChecking\sask/StrictHostKeyChecking no/' /etc/ssh/ssh_config

### supervisord
COPY src/docker/supervisord.conf /etc/supervisord.conf
RUN mkdir -p /var/log/supervisor

### SSHD
RUN mkdir /var/run/sshd
EXPOSE 22

# add jenkins user
RUN groupadd -g 496 jenkins
RUN mkdir -p /home/jenkins
RUN useradd --uid 495 -g 496 -p jenkins --home-dir /home/jenkins jenkins 
RUN chown jenkins:jenkins /home/jenkins

RUN mkdir /home/jenkins/.ssh
# move public key to enable ssh keys login
COPY src/docker/id_rsa.pub /home/jenkins/.ssh/authorized_keys
RUN chmod 400 /home/jenkins/.ssh/authorized_keys
RUN chown jenkins:jenkins /home/jenkins/.ssh/authorized_keys
RUN passwd -u jenkins

# create workspace dir
RUN mkdir /home/jenkins/workspace
RUN chown jenkins:jenkins /home/jenkins/workspace

# jenkins sudoer
RUN echo 'jenkins ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers
RUN sed -i "s/^.*requiretty/#Defaults requiretty/" /etc/sudoers

# Change sysstat conf
RUN sed -i -e 's|-S DISK|-S XDISK|' /etc/default/sysstat
RUN sed -i -e 's|*/10|*/3|' /etc/cron.d/sysstat

# Install cdab-tools
RUN mkdir /home/jenkins/cdab-tools
COPY src/docker/traceroute.py /home/jenkins/cdab-tools/
RUN chmod 755 /home/jenkins/cdab-tools/traceroute.py
COPY src/docker/sources.json /home/jenkins/cdab-tools/
COPY src/docker/sadf.sh /home/jenkins/cdab-tools/
COPY src/docker/sadf_in.py /home/jenkins/cdab-tools/
RUN chmod 755 /home/jenkins/cdab-tools/sadf*
RUN chown -R jenkins:jenkins /home/jenkins/cdab-tools

####

# make the terminal prettier
RUN echo 'export PS1="[\u@docker] \W # "' >> /root/.bash_profile

# launch the supervisor
CMD ["sudo", "/usr/bin/supervisord", "-c", "/etc/supervisord.conf"]

USER jenkins
WORKDIR /home/jenkins