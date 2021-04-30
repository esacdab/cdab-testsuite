##
#
# setup:
#   - generate an ssh key to be used as an authentication key with `ssh-keygen` and call it `docker_ssh_rsa`
#   - make sure your current ssh agent has the identity file added
#     - $ eval `ssh-agent`
#     - $ ssh-add docker_ssh_rsa
#
# build: $ sudo docker build -t <you>/centos7-testsite.
#
# run: $ sudo docker run -it <you>/centos7-testsite  /bin/bash
#
# ssh access: $ ssh root@localhost -p 8022
#
##

FROM centos:7

# CDAB release and timestamp
ARG CDAB_RELEASE
ENV CDAB_RELEASE ${CDAB_RELEASE:-00}
RUN echo "Docker image built on $(date -u) - CDAB Release v${CDAB_RELEASE}" >/etc/cdabbuild

# install openssh server
RUN yum -y install openssh-server

# install openssh clients
RUN yum -y install openssh-clients

# make ssh directories
RUN mkdir /root/.ssh
RUN mkdir /var/run/sshd

# create host keys
RUN ssh-keygen -b 1024 -t rsa -f /etc/ssh/ssh_host_key
RUN ssh-keygen -b 1024 -t rsa -f /etc/ssh/ssh_host_rsa_key
RUN ssh-keygen -b 1024 -t dsa -f /etc/ssh/ssh_host_dsa_key

# move public key to enable ssh keys login
ADD id_rsa.pub /root/.ssh/authorized_keys
RUN chmod 400 /root/.ssh/authorized_keys
RUN chown root:root /root/.ssh/authorized_keys
# tell ssh to not use ugly PAM
RUN sed -i 's/UsePAM\syes/UsePAM no/' /etc/ssh/sshd_config
RUN sed -i 's/#PubkeyAuthentication\syes/PubkeyAuthentication yes/' /etc/ssh/sshd_config
RUN sed -i 's/#RSAAuthentication\syes/RSAAuthentication yes/' /etc/ssh/sshd_config
RUN sed -i 's/#   StrictHostKeyChecking\sask/StrictHostKeyChecking no/' /etc/ssh/ssh_config

# make the terminal prettier
RUN echo 'export PS1="[\u@docker] \W # "' >> /root/.bash_profile

# enable networking
RUN echo 'NETWORKING=yes' >> /etc/sysconfig/network

# install supervisord
RUN yum -y install epel-release python-setuptools
RUN pip install supervisor
RUN /usr/bin/echo_supervisord_conf > /etc/supervisord.conf
RUN mkdir -p /var/log/supervisor

# make supervisor run in foreground
RUN sed -i -e "s/^nodaemon=false/nodaemon=true/" /etc/supervisord.conf

# tell supervisor to include relative .ini files
RUN mkdir /etc/supervisord.d
RUN echo [include] >> /etc/supervisord.conf
RUN echo 'files = /etc/supervisord.d/*.ini' >> /etc/supervisord.conf

# add sshd program to supervisord config
RUN echo [program:sshd] >> /etc/supervisord.d/ssh.ini
RUN echo 'command=/usr/sbin/sshd -D' >> /etc/supervisord.d/ssh.ini
RUN echo  >> /etc/supervisord.d/ssh.ini

EXPOSE 22

# add jenkins user
RUN groupadd -g 496 jenkins
RUN useradd --uid 495 -g 496 jenkins

RUN mkdir /home/jenkins/.ssh

# move public key to enable ssh keys login
ADD id_rsa.pub /home/jenkins/.ssh/authorized_keys
RUN chmod 400 /home/jenkins/.ssh/authorized_keys
RUN chown jenkins:jenkins /home/jenkins/.ssh/authorized_keys
RUN passwd -uf jenkins

# make the terminal prettier
RUN echo 'export PS1="[\u@docker] \W # "' >> /home/jenkins/.bash_profile

# create workspace dir
RUN mkdir /home/jenkins/workspace
RUN chown jenkins:jenkins /home/jenkins/workspace

# install jenkins dependencies
RUN yum install -y git java-1.8.0-openjdk rpm-build which sudo

# install shunit
ADD shunit2-2.1.6-3.el6.noarch.rpm /tmp/shunit2-2.1.6-3.el6.noarch.rpm
RUN yum localinstall -y /tmp/shunit2-2.1.6-3.el6.noarch.rpm

# jenkins sudoer
RUN echo 'jenkins ALL=(ALL:ALL) ALL' >> /etc/sudoers
RUN sed -i "s/^.*requiretty/#Defaults requiretty/" /etc/sudoers

# Install mono
# RUN rpm --import "https://keyserver.ubuntu.com/pks/lookup?op=get&search=0x3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF"
RUN su -c 'curl https://download.mono-project.com/repo/centos7-stable.repo | tee /etc/yum.repos.d/mono-centos7-stable.repo'
RUN yum -y install mono-complete nuget gcc

# add binfmt program to supervisord config
RUN echo [program:binfmt] >> /etc/supervisord.d/binfmt.ini
RUN echo "command=echo ':CLR:M::MZ::/usr/bin/mono:' > /proc/sys/fs/binfmt_misc/register" >> /etc/supervisord.d/binfmt.ini
RUN echo 'startretries=0' >> /etc/supervisord.d/binfmt.ini
RUN echo  >> /etc/supervisord.d/binfmt.ini
RUN echo 'binfmt_misc /proc/sys/fs/binfmt_misc binfmt_misc none' >>/etc/fstab

# install cdab-client
ARG CDAB_CLIENT_RPM
ADD $CDAB_CLIENT_RPM /tmp/$CDAB_CLIENT_RPM
RUN yum localinstall -y /tmp/$CDAB_CLIENT_RPM

# install cdab-remote-client
ARG CDAB_REMOTE_CLIENT_RPM
RUN yum install -y centos-release-scl
ADD $CDAB_REMOTE_CLIENT_RPM /tmp/$CDAB_REMOTE_CLIENT_RPM
RUN yum localinstall -y /tmp/$CDAB_REMOTE_CLIENT_RPM

# Install Python
RUN yum install -y \
    python \
    python-all

# Install traceroute
RUN yum install -y traceroute

# Install sysstat
RUN yum install -y \ 
    sysstat \
    sed

# add sysstat program to supervisord config
RUN echo [program:sysstat] >> /etc/supervisord.d/sysstat.ini
RUN echo 'command=/usr/lib64/sa/sa1 --boot' >> /etc/supervisord.d/sysstat.ini
RUN echo  >> /etc/supervisord.d/sysstat.ini

# change sysstat conf
RUN sed -i -e 's|-S DISK|-S XDISK|' /etc/sysconfig/sysstat

# add crond program to supervisord config
RUN sed -i -e 's|*/10|*/3|' /etc/cron.d/sysstat
RUN echo [program:crond] >> /etc/supervisord.d/crond.ini
RUN echo 'command=/usr/sbin/crond -n' >> /etc/supervisord.d/crond.ini
RUN echo  >> /etc/supervisord.d/crond.ini

# Install cdab-tools
RUN mkdir /home/jenkins/cdab-tools
ADD traceroute.py /home/jenkins/cdab-tools/
RUN chmod 755 /home/jenkins/cdab-tools/traceroute.py
ADD sources.json /home/jenkins/cdab-tools/
ADD sadf.sh /home/jenkins/cdab-tools/
ADD sadf_in.py /home/jenkins/cdab-tools/
RUN chmod 755 /home/jenkins/cdab-tools/sadf*
RUN chown -R jenkins:jenkins /home/jenkins/cdab-tools

# a complete update
RUN yum update -y

# launch the supervisor
CMD ["/usr/bin/supervisord", "-c", "/etc/supervisord.conf"]
