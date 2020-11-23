working_dir="$1"
docker_image="$2"
target_name="$3"
target_url="$4"
target_credentials="$5"
test_site="$6"
load_factor="$7"
download_origin="$8"
target_credentials_display=$(echo $target_credentials | sed -E 's/:.*/:xxxxxxxx/g')

cd "$working_dir"

echo "DOCKER COMMAND: docker run --workdir /res -v ${PWD}:/res --env DOWNLOAD_ORIGIN=${download_origin} \"${docker_image}\" mono $MONO_OPTIONS /usr/lib/cdab-client/bin/cdab-testtools.exe --target_name \"${target_name}\" --target_url \"${target_url}\" --target_credentials \"${target_credentials_display}\" --testsite_name \"${test_site}\" -lf \"${load_factor}\" --conf /res/config.yaml -v TS12" > cdab.stderr
docker run --workdir /res -v ${PWD}:/res --env DOWNLOAD_ORIGIN=${download_origin} "${docker_image}" mono $MONO_OPTIONS /usr/lib/cdab-client/bin/cdab-testtools.exe --target_name "${target_name}" --target_url "${target_url}" --target_credentials "${target_credentials}" --testsite_name "${test_site}" -lf "${load_factor}" --conf /res/config.yaml -v TS12 > cdab.stdout 2>> cdab.stderr
res=$?
if [ $res -ne 0 ]
then
    cat cdab.stdout
    cat cdab.stderr >&2
    exit 1
fi
