
%define debug_package %{nil}
%define __jar_repack  %{nil}
%define _python_bytecompile_errors_terminate_build 0
%global __os_install_post %(echo '%{__os_install_post}' | sed -e 's!/usr/lib[^[:space:]]*/brp-python-bytecompile[[:space:]].*$!!g')

Name:           cdab-remote-client
Url:            https://github.com/Terradue/cdab-testsuite
License:        AGPLv3
Group:          Productivity/Networking/Web/Servers
Version:        1.62
Release:        %{_release}
Summary:        Copernicus Sentinels Data Access Worldwide Benchmark Test Remote Client
BuildArch:      noarch
Requires:       bash, coreutils, centos-release-scl, rh-python36
AutoReqProv:    no

%description
Copernicus Sentinels Data Access Worldwide Benchmark Test Remote Client

%prep

%build


%install
mkdir -p %{buildroot}/usr/bin
cp %{_sourcedir}/bin/cdab-remote-client %{buildroot}/usr/bin/
mkdir -p %{buildroot}/usr/lib/cdab-remote-client
cp -r %{_sourcedir}/libexec/* %{buildroot}/usr/lib/cdab-remote-client
mkdir -p %{buildroot}/usr/lib/cdab-remote-client/etc
cp -r %{_sourcedir}/etc %{buildroot}/usr/lib/cdab-remote-client/etc


%post
SUCCESS=0

# Install OpenStack client, Google Cloud Platform Python API and Amazon AWS EC2 Python API
/opt/rh/rh-python36/root/usr/bin/pip install --upgrade pip
/opt/rh/rh-python36/root/usr/bin/pip install pyyaml lxml netifaces
/opt/rh/rh-python36/root/usr/bin/pip install python-openstackclient==5.1.0
/opt/rh/rh-python36/root/usr/bin/pip install google-api-python-client boto3
/opt/rh/rh-python36/root/usr/bin/pip install python-cinderclient==2.2.0

# Add symlink to cdab-remote-client
[ ! -f /usr/lib/cdab-remote-client/etc/config.yaml ] && cp /usr/lib/cdab-remote-client/etc/config.yaml.sample /usr/lib/cdab-remote-client/etc/config.yaml

/opt/rh/rh-python36/root/usr/bin/pip install --upgrade pip

exit ${SUCCESS}

%postun


%clean
rm -rf %{buildroot}


%files
/usr/lib/cdab-remote-client/*
/usr/bin/cdab-remote-client

%changelog
