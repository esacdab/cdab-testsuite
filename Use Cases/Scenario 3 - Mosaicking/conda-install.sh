echo "Installing conda"

CONDA_DIR="/opt/anaconda"
CONDA_INSTALL_DIR=$1
[ -z "$CONDA_INSTALL_DIR" ] && CONDA_INSTALL_DIR=$CONDA_DIR

cd $(dirname $0)
MINIFORGE_VERSION=4.8.2-1
# SHA256 for installers can be obtained from https://github.com/conda-forge/miniforge/releases
SHA256SUM="4f897e503bd0edfb277524ca5b6a5b14ad818b3198c2f07a36858b7d88c928db"
URL="https://github.com/conda-forge/miniforge/releases/download/${MINIFORGE_VERSION}/Miniforge3-${MINIFORGE_VERSION}-Linux-x86_64.sh"
INSTALLER_PATH=/tmp/miniforge-installer.sh
# Make sure user's $HOME is not tampered with since this is run as root
unset HOME
yum install -y wget
wget --quiet $URL -O ${INSTALLER_PATH}
chmod +x ${INSTALLER_PATH}
# Check sha256 checksum
if ! echo "${SHA256SUM}  ${INSTALLER_PATH}" | sha256sum  --quiet -c -
then
    echo "sha256 mismatch for ${INSTALLER_PATH}, exiting!"
    exit 1
fi
bash ${INSTALLER_PATH} -b -p ${CONDA_INSTALL_DIR} > /dev/null 2> /dev/null
if [ "$CONDA_INSTALL_DIR" != "$CONDA_DIR" ]
then
    ln -fs $CONDA_INSTALL_DIR $CONDA_DIR
fi
export PATH="${CONDA_DIR}/bin:$PATH"
# Preserve behavior of miniconda - packages come from conda-forge + defaults
conda config --system --append channels defaults
conda config --system --append channels terradue
conda config --system --append channels eoepca
conda config --system --append channels r
# Do not attempt to auto update conda or dependencies
conda config --system --set auto_update_conda false
conda config --system --set show_channel_urls true
# Bug in conda 4.3.>15 prevents --set update_dependencies
echo 'update_dependencies: false' >> ${CONDA_DIR}/.condarc
# Avoid future changes to default channel_priority behavior
conda config --system --set channel_priority "flexible"
echo "Done (conda)"
