
%define debug_package %{nil}
%define __jar_repack  %{nil}
%define _python_bytecompile_errors_terminate_build 0
%global __os_install_post %(echo '%{__os_install_post}' | sed -e 's!/usr/lib[^[:space:]]*/brp-python-bytecompile[[:space:]].*$!!g')

Name:           cdab-remote-client
Url:            https://github.com/Terradue/cdab-testsuite
License:        AGPLv3
Group:          Productivity/Networking/Web/Servers
Version:        1.69
Release:        %{_release}
Summary:        Copernicus Sentinels Data Access Worldwide Benchmark Test Remote Client
BuildArch:      noarch
Requires:       bash, coreutils, centos-release-scl
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

# Install OpenStack client, Google Cloud Platform Python API, Amazon AWS EC2 Python API and Microsoft Azure Python API
/usr/local/bin/pip3.7 install --upgrade pip
/usr/local/bin/pip3.7 install pyyaml lxml netifaces
/usr/local/bin/pip3.7 install python-openstackclient==5.1.0
/usr/local/bin/pip3.7 install python-cinderclient==2.2.0
/usr/local/bin/pip3.7 install google-api-python-client boto3
/usr/local/bin/pip3.7 install azure-identity azure-mgmt-resource azure-mgmt-authorization azure-mgmt-compute azure-mgmt-network urllib3==1.26.6

# Add symlink to cdab-remote-client
[ ! -f /usr/lib/cdab-remote-client/etc/config.yaml ] && cp /usr/lib/cdab-remote-client/etc/config.yaml.sample /usr/lib/cdab-remote-client/etc/config.yaml

/usr/local/bin/pip3.7 install --upgrade pip

exit ${SUCCESS}

%postun


%clean
rm -rf %{buildroot}


%files
/usr/lib/cdab-remote-client/*
/usr/bin/cdab-remote-client

%changelog
