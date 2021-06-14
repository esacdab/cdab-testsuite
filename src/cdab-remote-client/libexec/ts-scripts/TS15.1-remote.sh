function prepare() {
    echo "Installing tools" >> cdab.stderr
    sudo yum install -y bc unzip gcc python3-devel
    echo "Done" >> cdab.stderr

    echo "Installing Stars docker image" >> cdab.stderr
    docker pull $stage_in_docker_image
    echo "Done" >> cdab.stderr

    echo "Installing application docker image" >> cdab.stderr
    docker pull $application_docker_image
    echo "Done" >> cdab.stderr

    mkdir -p input_data
    mkdir -p output_data

    touch cdab.stdout
    touch cdab.stderr
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

    if [ "$provider" == "AMAZON" ] || [ "$provider" == "GOOGLE" ]
    then
        download_url=$(curl $ref | xmllint --format - | grep "<link rel=\"enclosure" | grep -E "scihub|apihub|copernicus\.eu" | sed -E "s#.*href=\"(.*?)\".*#\1#g")
        mkdir -p input_data/$id
        curl -u $credentials -o "${id}.zip" $download_url
        unzip -d input_data/$id "${id}.zip"
        res=$?
        [ $res -ne 0 ] && return $res
    else
        echo "docker run -u root --workdir /res -v ${PWD}:/res -v ${HOME}/config/etc/Stars:/etc/Stars/conf.d -v ${HOME}/config/Stars:/root/.config/Stars \"${stage_in_docker_image}\" Stars copy -v \"${ref}\" -r 4 -si ${provider} -o /res/input_data/ --allow-ordering --harvest" >> cdab.stderr
        docker run -u root --workdir /res -v ${PWD}:/res -v ${HOME}/config/etc/Stars:/etc/Stars/conf.d -v ${HOME}/config/Stars:/root/.config/Stars "${stage_in_docker_image}" Stars copy -v "${ref}" -r 4 -si ${provider} -o /res/input_data/ --allow-ordering --harvest >> cdab.stdout 2>> cdab.stderr
        res=$?
        [ $res -ne 0 ] && return $res
    fi
    
    return $res
}


# Read parameters
working_dir="$1"
application_docker_image="$2"
test_site="$3" # e.g. CREO
provider="$4"
credentials="$5"
index="sentinel2"
product_type="S2MSI1C"
product_count=2

stage_in_docker_image=terradue/stars-t2:0.5.38
[ -z "$application_docker_image" ] && application_docker_image=docker.terradue.com/cdab-ndvi:latest

cd "$1"

# Install
prepare

# Select input products
select_input

# Process inputs one by one

total_processings=0
wrong_processings=0

start_time=$(date +%s%N)

for id in $(cat input)
do
    ((total_processings++))

    echo "Stage in $id" >> cdab.stderr
    # Stage in input product
    stage_in $id >> cdab.stdout 2>> cdab.stderr
    res=$?
    echo "EXIT CODE = $res" >> cdab.stderr

    if [ $res -ne 0 ]
    then
        ((wrong_processings++))
        echo "Stage in of $id FAILED" >> cdab.stderr
        continue
    fi
    # Run tool

    path=$(find ./input_data -type d -name IMG_DATA | grep $id)/

    echo "Docker command: docker run -i --user=root --workdir /workdir -v ${PWD}:/workdir $application_docker_image /ndvi.py \"${path}\" /workdir/output_data " >> cdab.stderr
    docker run -i --user=root --workdir /workdir -v ${PWD}:/workdir $application_docker_image /ndvi.py "${path}" /workdir/output_data >> cdab.stdout 2>> cdab.stderr
    res=$?
    echo "EXIT CODE = $res" >> cdab.stderr

    if [ $res -ne 0 ]
    then
        ((wrong_processings++))
        echo "Processing of $id FAILED" >> cdab.stderr
    else
        ls -l output_data/*.tif >> cdab.stderr
        errors=0
        count=0
        for file in $(ls output_data/*.tif)
        do
            ((count++))
            [ ! -s $file ] && errors=1
        done

        if [ ${errors} -ne 0 ] || [ ${count} -eq 0 ]
        then
            ((wrong_processings++))
        fi
    fi
    rm -f output_data/*.tif
done

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
