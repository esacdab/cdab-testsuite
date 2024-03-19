working_dir="$1"
docker_image="$2"
target_name="$3"
target_url="$4"
target_credentials="$5"
test_site="$6"
load_factor="$7"
download_origin="$8"
target_credentials_display=$(echo $target_credentials | sed -E 's/:.*/:xxxxxxxx/g')

if [ -d "/eodata" ]
then
    eodata_volume_option=" -v /eodata:/eodata"
else
    eodata_volume_option=""
fi

cd "$working_dir"

# run the docker detached
docker run --detach --name ${test_site} ${eodata_volume_option} --env DOWNLOAD_ORIGIN=${download_origin} "${docker_image}"
# copy the config file to the docker
docker cp ${PWD}/config.yaml ${test_site}:/home/jenkins/config.yaml
# run the test
echo "DOCKER COMMAND: docker exec --workdir /home/jenkins -it ${test_site} /usr/lib/cdab-client/cdab-testtools --target_name \"${target_name}\" --target_url \"${target_url}\" --target_credentials \"${target_credentials_display}\" --testsite_name \"${test_site}\" -lf \"${load_factor}\" --conf /home/jenkins/config.yaml -v TS11" > cdab.stderr
docker exec --workdir /home/jenkins -t ${test_site} /usr/lib/cdab-client/cdab-testtools --target_name "${target_name}" --target_url "${target_url}" --target_credentials "${target_credentials}" --testsite_name "${test_site}" -lf "${load_factor}" --conf /home/jenkins/config.yaml -v TS11 > cdab.stdout 2>> cdab.stderr
res=$?
if [ $res -ne 0 ]
then
    cat cdab.stdout
    cat cdab.stderr >&2
    exit 1
fi
# Copy the results back to the working directory
docker cp ${test_site}:/home/jenkins/TS11Results.json ${PWD}/TS11Results.json
docker cp ${test_site}:/home/jenkins/junit.xml ${PWD}/junit.xml
# Delete the container
docker rm -f ${test_site}
