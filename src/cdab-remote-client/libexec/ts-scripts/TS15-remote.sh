function prepare() {
    echo "Installing basic packages"
    sudo apt-get install -y bc wget unzip libgfortran5-amd64-cross
    echo "Done (basic packages)"

    echo "Installing conda"
    CONDA_DIR=/opt/anaconda
    cd $(dirname $0)
    MINIFORGE_VERSION=4.8.2-1
    # SHA256 for installers can be obtained from https://github.com/conda-forge/miniforge/releases
    SHA256SUM="4f897e503bd0edfb277524ca5b6a5b14ad818b3198c2f07a36858b7d88c928db"
    URL="https://github.com/conda-forge/miniforge/releases/download/${MINIFORGE_VERSION}/Miniforge3-${MINIFORGE_VERSION}-Linux-x86_64.sh"
    INSTALLER_PATH=/tmp/miniforge-installer.sh
    # Make sure user's $HOME is not tampered with since this is run as root
    unset HOME
    wget --quiet $URL -O ${INSTALLER_PATH}
    chmod +x ${INSTALLER_PATH}
    # Check sha256 checksum
    if ! echo "${SHA256SUM}  ${INSTALLER_PATH}" | sha256sum  --quiet -c -; then
        echo "sha256 mismatch for ${INSTALLER_PATH}, exiting!"
        exit 1
    fi
    bash ${INSTALLER_PATH} -b -p ${CONDA_DIR}
    export PATH="${CONDA_DIR}/bin:$PATH"
    # Preserve behavior of miniconda - packages come from conda-forge + defaults
    conda config --system --append channels defaults
    conda config --system --append channels https://conda.binstar.org/terradue
    conda config --system --append channels https://conda.binstar.org/eoepca
    conda config --system --append channels https://conda.binstar.org/r
    # Do not attempt to auto update conda or dependencies
    conda config --system --set auto_update_conda false
    conda config --system --set show_channel_urls true
    # bug in conda 4.3.>15 prevents --set update_dependencies
    echo 'update_dependencies: false' >> ${CONDA_DIR}/.condarc
    # avoid future changes to default channel_priority behavior
    conda config --system --set channel_priority "flexible"
    echo "Done (conda)"

    echo "Creating conda environment with snap and cwltool"
    conda create -n env_snap -y snap cwltool
    conda activate env_snap
    echo "Done (conda environment)"

    echo "Installing Stars docker image"
    docker pull $stage_in_docker_image
    echo "Done (Stars)"

    mkdir input_data

}


function select_input() {
    # If no inputs are given, select random files from a week to two weeks ago
    if [ ! -s "input" ]
    then
        echo "Selecting input from catalogue" >> cdab.stderr
        now=$(date +%s)
        start=$(date -d@$((now - 14 * 24 * 60 * 60)) +%Y-"%m-%dT00:00:00Z")
        end=$(date -d@$((now - 7 * 24 * 60 * 60)) +%Y-"%m-%dT00:00:00Z")
        curl "https://catalog.terradue.com/${index}/search/?pt=${product_type}&bbox=5,40,15,50&start=${start}&stop=${end}" | \
            xmllint --format - | grep -E '<link rel="self".*uid' | sed -E 's/^.*uid=(.+)".*/\1/g' | head -${product_count} \
            > input
        echo "Done" >> cdab.stderr
    else
        echo "Using provided input file" >> cdab.stderr
    fi
}


function stage_in() {
    id=$1

    # Temporarily using Terradue catalogue for query
    ref="https://catalog.terradue.com/${index}/search?uid=$id"

    echo "docker run -u root --workdir /res -v ${PWD}:/res -v ${PWD}/config/Stars:/root/.config/Stars \"${stage_in_docker_image}\" Stars -v copy \"${ref}\" -r 4 -si ${provider} -o /res/input_data/"
    docker run -u root --workdir /res -v ${PWD}:/res -v ${PWD}/config/Stars:/root/.config/Stars "${stage_in_docker_image}" Stars -v copy "${ref}" -r 4 -si ${provider} -o /res/input_data/
    res=$?
    [ $res -ne 0 ] && return $res
    
    path=$(find ./input_data -type d -name IMG_DATA | grep $id)/

    cat > wfinput.yaml << EOF
s2_img_data_folder:
  class: Directory
  path: $path
EOF
    return $res
}


# Read parameters
working_dir="$1"
# 2nd argument is docker image ID, not used
test_site="$3" # e.g. CREO
provider="$4"
index="sentinel2"
product_type="S2MSI1C"
product_count=2

stage_in_docker_image=terradue/stars-t2:devlatest

cd "$working_dir"

# Install
prepare 2>> cdab.stderr

# Select input products
select_input 2>> cdab.stderr

# Process inputs one by one

total_processings=0
wrong_processings=0

start_time=$(date +%s%N)


end_time=$(date +%s%N)

process_duration=$(((end_time - start_time) / 1000000))

if [ ${total_processings} -eq 0 ]
then
    error_rate=100.0
    avg_process_duration=-1
else
    r=$(echo "scale=4; (${wrong_processings}*100)/${total_processings}+0.04999" | bc)
    error_rate=$(printf "%.1f" $r)
    correct_processings=$((total_processings - wrong_processings))
    if [ ${correct_processings} -eq 0 ]
    then
        avg_process_duration=-1
    else
        avg_process_duration=$((process_duration / correct_processings))
    fi
fi

cat > junit.xml << EOF
<?xml version="1.0"?>
<testsuites xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <testsuite name="Execution of a predefined processing scenario test" errors="${errors}" id="TS15">
  </testsuite>
</testsuites>
EOF

cat > TS15Results.json << EOF
{
  "jobName": null,
  "buildNumber": null,
  "testScenario": "TS15",
  "testSite": "${test_site}",
  "testTargetUrl": "${input_reference}",
  "testTarget": "${test_site}",
  "zoneOffset": null,
  "hostName": null,
  "hostAddress": null,
  "testCaseResults": [
    {
      "testName": "TC415",
      "className": "cdabtesttools.TestCases.TestCase415",
      "metrics": [
        { 
          "name": "errorRate",
          "value": ${error_rate},
          "uom": "%"
        },
        { 
          "name": "processDuration",
          "value": ${process_duration},
          "uom": "ms"
        },
        { 
          "name": "avgProcessDuration",
          "value": ${avg_process_duration},
          "uom": "ms"
        },
        { 
          "name": "processCount",
          "value": ${total_processings},
          "uom": "#"
        }
      ]
    }
  ]
}
EOF
