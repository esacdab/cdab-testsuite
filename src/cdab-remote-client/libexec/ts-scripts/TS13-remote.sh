function prepare() {
    echo "Installing tools" >> cdab.stderr
    sudo yum install -y bc unzip
    echo "Done" >> cdab.stderr

    mkdir -p input_data

    touch cdab.stdout
    touch cdab.stderr
}

function select_input() {
    # If no inputs are given, select random files from less than a week ago
    if [ ! -s "input" ]
    then
        echo "Selecting input from catalogue" >> cdab.stderr
        now=$(date +%s)
        start=$(date -d@$((now - 6 * 24 * 60 * 60)) +%Y-"%m-%dT00:00:00Z")
        end=$(date -d@$((now - 5 * 24 * 60 * 60)) +%Y-"%m-%dT00:00:00Z")
        curl "https://catalog.terradue.com/sentinel3/search/?pt=OL_1_EFR___&start=${start}&stop=${end}&count=1&do=terradue" | \
            xmllint --format - > result.atom.xml
        grep "<eop:identifier>" result.atom.xml | sed -E "s#.*<.*?>(.*)<.*>.*#\1#g" > input

    else
        echo "Using provided input file" >> cdab.stderr
    fi

    if [ ! -s input ]
    then
        echo "Empty product list" >> cdab.stderr
        return 1
    fi

    echo "Done" >> cdab.stderr
}



function stage_in() {
    id=$1
    curl "https://catalog.terradue.com/sentinel3/search/?uid=${id}&do=terradue" | \
        xmllint --format - > current.atom.xml

    download_url=$(grep "<link rel=\"enclosure" current.atom.xml | grep "copernicus.eu" | sed -E "s#.*href=\"(.*?)\".*#\1#g")
    echo "DOWNLOAD URL = $download_url" >> cdab.stderr >> cdab.stderr

    if [ -z "$download_url" ]
    then
        echo "No download URL found" >> cdab.stderr
        return 1
    fi
    
    curl -L -v -u "${apihub_credentials}" -o "${id}.zip" ${download_url}
    unzip -o -d input_data "${id}.zip"
    if [ $? -ne 0 ]
    then
	echo "Error while unzipping file" >> cdab.stderr
        return 1
    fi

    sen_folder=$(find input_data -type d -name "*.SEN3")/

    return $res
}


working_dir="$1"
[ "$working_dir" == "." ] && working_dir=$HOME

docker_image="$2"   # docker-co.terradue.com/geohazards-tep/ewf-s3-olci-composites:0.41
test_site="$3"   # e.g. CREO
provider="$4"
target_credentials=$5   # not used
apihub_credentials="$6"   # API Hub credentials

cd "$working_dir"

# Install
prepare 2>> cdab.stderr
res=$?

if [ $res -eq 0 ]
then
    # Select input products
    select_input 2>> cdab.stderr
    res=$?
fi

# Process input
total_processings=0
wrong_processings=0
total_process_duration=0

for id in $(cat input)
do
    ((total_processings++))

    echo "Stage in $id" >> cdab.stderr
    # Stage in input product
    stage_in $id
    res=$?
    echo "EXIT CODE = $res" >> cdab.stderr
    if [ $res -eq 0 ]
    then
        input_reference="https://catalog.terradue.com/sentinel3/search?uid=${id}"
    else
        ((wrong_processings++))
        echo "Stage in of $id FAILED" >> cdab.stderr
        continue
    fi

    start_time=$(date +%s%N)
    if [ $res -eq 0 ]
    then
        echo "DOCKER COMMAND: docker run --memory=15g --rm --workdir /res -u root -v ${PWD}:/res \"${docker_image}\" /opt/anaconda/envs/env_ewf_s3_olci_composites/bin/python s3-olci-composites.py /res/current.atom.xml /res/${sen_folder}" >> cdab.stderr
        docker run --memory=15g --rm --workdir /res -u root -v ${PWD}:/res "${docker_image}" /opt/anaconda/envs/env_ewf_s3_olci_composites/bin/python s3-olci-composites.py /res/current.atom.xml /res/${sen_folder} > cdab.stdout 2>> cdab.stderr
        res=$?
        echo "EXIT CODE = $res" >> cdab.stderr
    fi
    end_time=$(date +%s%N)

    ls -l S3-OLCI-* >> cdab.stderr

    if [ $res -ne 0 ]
    then
        cat cdab.stdout
        cat cdab.stderr >&2
        ((wrong_processings++))
        echo "Processing of $id FAILED" >> cdab.stderr
        echo "Files:" >> cdab.stderr
        for dir in $(find . -type d); do echo "--------" >> cdab.stderr; echo $dir >> cdab.stderr; ls -l $dir >> cdab.stderr; done
    else
        ls -l *.tif >> cdab.stderr
        errors=0
        count=0
        for file in $(ls S3-OLCI-*)
        do
            ((count++))
            [ ! -s $file ] && errors=1
        done

        if [ ${errors} -ne 0 ] || [ ${count} -eq 0 ]
        then
            ((wrong_processings++))
        fi
    fi
    rm -f S3-OLCI-*

    process_duration=$(((end_time - start_time) / 1000000))
    total_process_duration=$((total_process_duration + process_duration))
done


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
        avg_process_duration=$((total_process_duration / correct_processings))
    fi
fi

cat > junit.xml << EOF
<?xml version="1.0"?>
<testsuites xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <testsuite name="Simple data transformation" errors="${errors}" id="TS13">
    <testcase name="TC413" classname="cdabtesttools.TestCases.TestCase413" status="${status}" />
  </testsuite>
</testsuites>
EOF

cat > TS13Results.json << EOF
{
  "jobName": null,
  "buildNumber": null,
  "testScenario": "TS13",
  "testSite": "${test_site}",
  "testTargetUrl": "${input_reference}",
  "testTarget": "${provider}",
  "zoneOffset": null,
  "hostName": null,
  "hostAddress": null,
  "testCaseResults": [
    {
      "testName": "TC413",
      "className": "cdabtesttools.TestCases.TestCase415",
      "startedAt": "$(date -d @${start_time::-9} +%Y-"%m-%dT%H:%M:%SZ")",
      "endedAt": "$(date -d @${end_time::-9} +%Y-"%m-%dT%H:%M:%SZ")",
      "duration": ${process_duration},
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
