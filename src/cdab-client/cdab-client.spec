
%define debug_package %{nil}
%define __jar_repack  %{nil}

Name:           cdab-client
Url:            https://git.terradue.com/systems/cdab-cli
License:        AGPLv3
Group:          Productivity/Networking/Web/Servers
Version:        1.3.1
Release:        %{_release}
Summary:        Copernicus Sentinels Data Access Worldwide Benchmark Test Client
BuildArch:      noarch
Source:         /usr/bin/cdab-client
Requires:       mono
AutoReqProv:    no
# BuildRequires:  libtool


%description
Copernicus Sentinels Data Access Worldwide Benchmark Test Client

%prep

%build


%install
mkdir -p %{buildroot}/usr/lib/cdab-client/bin
cp -r %{_sourcedir}/bin/*/net4*/*.dll %{buildroot}/usr/lib/cdab-client/bin
cp -r %{_sourcedir}/bin/*/net4*/*.exe %{buildroot}/usr/lib/cdab-client/bin
cp -r %{_sourcedir}/App_Data %{buildroot}/usr/lib/cdab-client
mkdir -p %{buildroot}/usr/bin/
cp %{_sourcedir}/cdab-client %{buildroot}/usr/bin/


%post

%postun


%clean
rm -rf %{buildroot}


%files
/usr/lib/cdab-client/*
/usr/bin/cdab-client

%changelog
