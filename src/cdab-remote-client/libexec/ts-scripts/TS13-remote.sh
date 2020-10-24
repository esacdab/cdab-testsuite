function prepare() {
    echo "Installing tools" >> cdab.stderr
    sudo yum install -y bc unzip
    echo "Done" >> cdab.stderr

    echo "Installing Stars docker image" >> cdab.stderr
    docker pull $stage_in_docker_image
    echo "Done" >> cdab.stderr

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


working_dir="$1"
docker_image="$2" # docker-co.terradue.com/geohazards-tep/ewf-s3-olci-composites:0.41
test_site="$3" # e.g. CREO
provider="$4"
index="sentinel3"
product_type="OL_1_EFR___"
product_count=1

stage_in_docker_image=terradue/stars-t2:devlatest

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
    stage_in $id > cdab.stdout 2>> cdab.stderr
    res=$?
    echo "EXIT CODE = $res" >> cdab.stderr

    if [ $res -ne 0 ]
    then
        ((wrong_processings++))
        echo "Stage in of $id FAILED" >> cdab.stderr
        continue
    fi

    input_reference="https://catalog.terradue.com/sentinel3/search?uid=${id}"

    # Run tool
    echo "DOCKER COMMAND: docker run --memory=15g --rm --workdir /res -u root -v ${PWD}:/res \"${docker_image}\" ellip-nb-run /application/s3-olci-composites/s3-olci-composites-run.ipynb --stage-in Yes --input_reference  \"${input_reference}\"" >> cdab.stderr
    docker run --memory=15g --rm --workdir /res -u root  -v ${PWD}:/res "${docker_image}" ellip-nb-run /application/s3-olci-composites/s3-olci-composites-run.ipynb --stage-in Yes --input_reference  "${input_reference}" > cdab.stdout 2>> cdab.stderr
    res=$?
    # if [ $res -ne 0 ]
    # then
    #     cat cdab.stdout
    #     cat cdab.stderr >&2
    #     exit 1
    # fi

    echo "EXIT CODE = $res" >> cdab.stderr

    ls -l S3-OLCI-* >> cdab.stderr

    if [ $res -ne 0 ]
    then
        cat cdab.stdout
        cat cdab.stderr >&2
        ((wrong_processings++))
        echo "Processing of $id FAILED" >> cdab.stderr
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
  <testsuite name="Simple data transformation" errors="${errors}" id="TS13">
    <testcase name="TC403r" classname="cdabtesttools.TestCases.TestCase403r" status="${status}" />
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
    ]
}
EOF
