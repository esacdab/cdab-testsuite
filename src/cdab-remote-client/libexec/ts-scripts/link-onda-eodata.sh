yum install -y nfs-utils
mkdir /eodata
sudo mount -t nfs4 -o nfsvers=4.1 ens-legacy.onda-dias.eu:/ /eodata
