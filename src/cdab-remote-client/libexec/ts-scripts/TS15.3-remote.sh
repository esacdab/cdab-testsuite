function prepare() {
    tar xvzf s3-olci.tgz

    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Installing basic packages" >> cdab.stderr
    sudo apt-get install -y bc wget unzip libgfortran5-amd64-cross >> cdab.stderr 2>&1
    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done (basic packages)" >> cdab.stderr

    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Creating conda environment with snap and cwltool" >> cdab.stderr
    CONDA_DIR="/opt/anaconda"
    CONDA_PREFIX="${PWD}/env_s3"

    sudo $CONDA_DIR/bin/conda env create -p $PWD/env_s3 --file environment.yml
    sudo ln -s "${CONDA_PREFIX}" "${CONDA_DIR}/envs/env_s3"
    sudo chown -R $USER:$USER ${CONDA_PREFIX}/snap/
    export PATH="${CONDA_DIR}/bin:${CONDA_PREFIX}/bin:${CONDA_PREFIX}/snap/bin:${CONDA_PREFIX}/snap/jre/bin:$PATH"
    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done (conda environment)" >> cdab.stderr

    mkdir -p input_data
    mkdir -p output_data
}


function select_input() {
    # If no inputs are given, select random files from a week to two weeks ago
    if [ -s "$PWD/input" ]
    then
        if grep -q = "$PWD/input"   # input exists and contains query parameters ('=' signs)
        then
            . $PWD/input
        else   # input exists and contains identifiers (no '=' signs)
            cp "$PWD/input" ids.list
            return
        fi
    else
        ref_date_sec=$(date +%s)
        start_date=$(date -d@$((ref_date_sec - 14 * 24 * 60 * 60)) +%Y-"%m-%dT00:00:00Z")
        end_date=$(date -d@$((ref_date_sec + 0)) +%Y-"%m-%dT00:00:00Z")
        bbox="-20,5,10,35"
        geom="POLYGON((-20 5,10 5,10 35,-20 35,-20 5))"
        count=20

        [ -s "$PWD/input" ] && . $PWD/input
    fi

    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Selecting post-event product from catalogue" >> cdab.stderr

    case "$provider" in
        CREO)
            search_params="-p pt=LFR"
            ;;
        ONDA)
            search_params="-p pt=LFR"
            ;;
        MUNDI)
            search_params="-p psn=S3 -p pt=OL_2_LFR___"
            ;;
        SOBLOO)
            search_params="-p pt=OL_2_LFR___"
            ;;
        *)
            search_params="-p pt=OL_2_LFR___"
            ;;
    esac

    if [ "$provider" == "WEKEO" ]
    then
        $PWD/env_s3/bin/python wekeo-tool.py query --credentials="$credentials" --pn=Sentinel-3 --pt=OL_2_LFR___ --bbox="$bbox" --dates="${start_date}/${end_date}" > urls.list 2>> cdab.stderr
        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done ($(cat urls.list | wc -l) items)" >> cdab.stderr
    else
        echo "docker run --rm terradue/opensearch-client opensearch-client -m Scihub -p start=${start_date} -p stop=${end_date} -p \"geom=${geom}\" $search_params -p count=$count \"${catalogue_base_url}\" {}" >> cdab.stderr
        docker run --rm terradue/opensearch-client opensearch-client $cat_creds -m Scihub -p start=${start_date} -p stop=${end_date} -p "geom=${geom}" $search_params -p count=$count "${catalogue_base_url}" {} | xmllint --format - > result.atom.xml
        if [ $? -ne 0 ]
        then
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Invalid or empty result from ${catalogue_base_url}" >> cdab.stderr
            return 1
        fi

        grep "<dc:identifier>" result.atom.xml | sed -E "s#.*<.*?>(.*)<.*>.*#\1#g" > ids.list

        # if [ ${provider} == "ONDA" ] && [ $(grep -E "^S3[AB]_OL_2_LFR____" ids.list | wc -l) -eq 0 ]
        if [ ${provider} == "ONDA" ]   # workaround until ENS is working again
        then
            grep "<link rel=\"enclosure\" " result.atom.xml | sed -E "s#.*href=\"(.+?)\".*#\1#g" > urls.list
        fi
        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done ($(cat ids.list | wc -l) items)" >> cdab.stderr
    fi
}


function process_mosaic() {
    max_attempts=1
    attempt=0

    if [ -s urls.list ]
    then
        item_type='url'
        list_file=urls.list
    else
        item_type='id'
        list_file=ids.list
    fi

    while [ $attempt -lt $max_attempts ]
    do
        ((attempt++))
        download $item_type $list_file $product_count

        missing=$?

        if [ $missing -eq 0 ]
        then
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - All files downloaded" >> cdab.stderr
            break
        else
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Files missing: ${missing}/${product_count}" >> cdab.stderr
            break
        fi
    done
    errors=$missing

    tree --charset ascii $PWD/input_data >> cdab.stderr

    $PWD/env_s3/bin/python s3_olci_mosaic.py $PWD/input_data/ 0.5 $PWD/output_data/ 

    tree --charset ascii $PWD/output_data >> cdab.stderr
    ls -l $PWD/output_data >> cdab.stderr

    if [ ! -s output_data/mosaic.tif ] || [ ! -s output_data/ndvi_rgba.tif ]
    then
        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Output files missing" >> cdab.stderr
        errors=$product_count
    fi

    return
}



function download() {

    item_type=$1
    list_file=$2
    size=$3

    count=0
    missing=0

    for id in $(cat $list_file)
    do
        ((count++))
        error=

        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Processing product ${count}/${size}: ${id}" >> cdab.stderr
        if [ "$provider" == "WEKEO" ]
        then
            res=0
            if [ "$item_type" == 'id' ]
            then
                python3 wekeo-tool.py query --credentials="$credentials" --pn=Sentinel-3 --pt=OL_2_LFR___ --uid=$id > download.url 2>> cdab.stderr
                if [ -s download.url ]
                then
                    url=$(head -1 download.url)
                    product_id=$id
                else
                    echo "Not found on WEkEO: ${id}" >> cdab.stderr
                    res=1
                fi
            else
                url=$id
                product_id=$(echo $url | sed -E "s/.*(S3[AB][A-Z0-9_]+)\.SEN3.*/\1/")
            fi
            if [ $res -eq 0 ]
            then
                echo "Download from WEkEO: ${count}/${size}: ${id}" >> cdab.stderr
                $PWD/env_s3/bin/python wekeo-tool.py download --credentials="$credentials" --url="$url" --dest="${product_id}.zip" 2>> cdab.stderr
                unzip -d $PWD/input_data/ "${product_id}.zip"
                res=$?
                rm -f "${product_id}.zip"
            fi
        else
            # Try staging in with direct method (DIAS-specific)
            echo "Stage-in command: $PWD/env_s3/bin/python stage-in.py \"$item_type\" \"OL_2_LFR___\" \"$provider\" \"$id\" $PWD/input_data/ \"...\" \"...\"" >> cdab.stderr
            $PWD/env_s3/bin/python stage-in.py "$item_type" "OL_2_LFR___" "$provider" "$id" $PWD/input_data/ "$credentials" "$backup_credentials" 2>> cdab.stderr
            res=$?
        fi

        if [ $res -eq 0 ]
        then
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done (product $count/$size downloaded)" >> cdab.stderr
            continue
        fi

        ((missing++))

    done

    return $missing

}


# Read parameters
working_dir="$1"
[ "$working_dir" == "." ] && working_dir=$HOME

# 2nd argument is docker image ID, not used
test_site="$3" # e.g. CREO
provider="$4"
credentials=$(echo $5 | sed -E 's# .*##g')
backup_credentials=$(echo $6 | sed -E 's# .*##g')
cat_creds=""

stage_in_docker_image=terradue/stars:1.3.6

case "$provider" in
    CREO)
        catalogue_base_url="https://finder.creodias.eu/resto/api/collections/Sentinel3/describe.xml"
        ;;
    MUNDI)
        catalogue_base_url="https://sentinel3.browse.catalog.mundiwebservices.com/opensearch"
        cat_creds="-a $credentials"
        ;;
    ONDA)
        catalogue_base_url="https://catalogue.onda-dias.eu/dias-catalogue"
        ;;
    SOBLOO)
        catalogue_base_url="https://sobloo.eu/api/v1/services/search"
        ;;
    *)
        catalogue_base_url="https://scihub.copernicus.eu/dhus/odata/v1"
        cat_creds="-a $credentials"
        ;;
esac

cd "$working_dir"

printf "" > cdab.stdout
printf "" > cdab.stderr

# Install
prepare
res=$?

if [ $res -eq 0 ]
then
    # Select input products
    select_input 2>> cdab.stderr
    res=$?
fi

product_count=0

# Process inputs

echo "--------------------------------" >> cdab.stderr
echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - TC415: Mosaic" >> cdab.stderr

errors=0

start_time_mosaic=$(date +%s%N)

if [ $res -eq 0 ]
then
    if [ -s urls.list ]
    then
        product_count=$(cat urls.list | wc -l)
    else
        product_count=$(cat ids.list | wc -l)
    fi
    process_mosaic
fi

end_time_mosaic=$(date +%s%N)

process_duration_mosaic=$(((end_time_mosaic - start_time_mosaic) / 1000000))

total_processings_mosaic=1

if [ ${product_count} -eq 0 ]
then
    error_rate_mosaic=100.0
elif [ ${errors} -eq 0 ]
then
    error_rate_mosaic=0.0
else
    r=$(echo "scale=4; (${errors}*100)/${product_count}+0.04999" | bc)
    error_rate_mosaic=$(printf "%.1f" $r)
fi  
avg_process_duration_mosaic=$process_duration_mosaic



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
      "startedAt": "$(date -d @${start_time_mosaic::-9} +%Y-"%m-%dT%H:%M:%SZ")",
      "endedAt": "$(date -d @${end_time_mosaic::-9} +%Y-"%m-%dT%H:%M:%SZ")",
      "duration": ${process_duration_mosaic},
      "metrics": [
        { 
          "name": "errorRate",
          "value": ${error_rate_mosaic},
          "uom": "%"
        },
        { 
          "name": "processDuration",
          "value": ${process_duration_mosaic},
          "uom": "ms"
        },
        { 
          "name": "avgProcessDuration",
          "value": ${avg_process_duration_mosaic},
          "uom": "ms"
        },
        { 
          "name": "processCount",
          "value": ${total_processings_mosaic},
          "uom": "#"
        }
      ]
    }
  ]
}
EOF
