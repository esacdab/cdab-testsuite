working_dir="$1"
docker_image="$2" # docker-co.terradue.com/geohazards-tep/ewf-s3-olci-composites:0.41
test_site="$3" # CREO
download_origin=terradue

cd "$1"

uid=$(curl "https://catalog.terradue.com/sentinel3/search/?pt=OL_1_EFR___&bbox=3,44,23,54" | xmllint --format - | grep -E '<link rel="self".*uid' | sed -E 's/^.*uid=(.+)".*/\1/g' | head -1)

if [ -z $uid ]
then
    echo "No product UID found" > cdab.stderr
    exit 1
fi
uid="S3A_OL_1_EFR____20191110T230850_20191110T231150_20191112T030831_0179_051_215_3600_LN1_O_NT_002"
input_reference="https://catalog.terradue.com/sentinel3/search?uid=${uid}"

echo "LATEST PRODUCT IDENTIFIER: ${uid}" > cdab.stderr
echo "DOCKER COMMAND: docker run --memory=15g --rm --workdir /res -u root  -v ${PWD}:/res --env DOWNLOAD_ORIGIN=${download_origin} \"${docker_image}\" ellip-nb-run /application/s3-olci-composites/s3-olci-composites-run.ipynb --stage-in Yes --input_reference  \"${input_reference}\"" >> cdab.stderr
start_time=$(date --iso-8601=ns)
docker run --memory=15g --rm --workdir /res -u root  -v ${PWD}:/res --env DOWNLOAD_ORIGIN=${download_origin} "${docker_image}" ellip-nb-run /application/s3-olci-composites/s3-olci-composites-run.ipynb --stage-in Yes --input_reference  "${input_reference}" > cdab.stdout 2>> cdab.stderr
res=$?
# if [ $res -ne 0 ]
# then
#     cat cdab.stdout
#     cat cdab.stderr >&2
#     exit 1
# fi
end_time=$(date --iso-8601=ns)

echo "EXIT CODE = $res" >> cdab.stderr

ls -l S3-OLCI-* >> cdab.stderr

errors=0
count=0
for file in $(ls S3-OLCI-*)
do
    ((count++))
    [ ! -s $file ] && errors=1
done

if [ ${errors} -eq 0 ] && [ ${count} -eq 4 ]
then
    status="OK"
else
    status="ERROR"
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
    "testTarget": "${download_origin}",
    "zoneOffset": null,
    "hostName": null,
    "hostAddress": null,
    "testCaseResults": [
    ]
}
EOF
