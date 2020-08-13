function stage_in() {
    path=

    for url in $(cat input)
    do
        filename=$(basename $url)
        curl -L -o $filename $url
        unzip $filename
        path="$(find . -type d -name IMG_DATA)/"
    done

    cat > wfinput.yaml << EOF
s2_img_data_folder:
  class: Directory
  path: $path
EOF
}


# Read parameters
working_dir="$1"
# 2nd argument is docker image ID, not used
test_site="$3" # CREO

cd "$1"

echo "Installing cwltool" >> cdab.stderr
sudo yum install -y unzip gcc python3-devel
sudo pip3 install cwltool
echo "Done" >> cdab.stderr

# Stage in input product
stage_in

error_rate="0.0"

# Run tool
echo "CWL Command: cwltool workflow.cwl#wf wfinput.yaml" >> cdab.stderr
start_time=$(date +%s%N)
cwltool workflow.cwl#wf wfinput.yaml > cdab.stdout 2>> cdab.stderr
res=$?
end_time=$(date +%s%N)

if [ $res -ne 0 ]
then
    cat cdab.stdout
    cat cdab.stderr >&2
    error_rate="100.0"
fi
echo "EXIT CODE = $res" >> cdab.stderr

process_duration=$(((end_time - start_time) / 1000000))

ls -l *.tif >> cdab.stderr

errors=0
count=0
for file in $(ls *.tif)
do
    ((count++))
    [ ! -s $file ] && errors=1
done

if [ ${errors} -ne 0 ] || [ ${count} -eq 0 ]
then
    error_rate="100.0"
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
        }
      ]
    }
  ]
}
EOF
