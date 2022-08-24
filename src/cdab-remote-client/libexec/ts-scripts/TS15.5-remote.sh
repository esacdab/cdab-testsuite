function prepare() {
    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Installing basic packages" >> cdab.stderr
    sudo yum install -y bc tree wget unzip libgfortran-4.8.5 >> cdab.stderr 2>&1
    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done (basic packages)" >> cdab.stderr

    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Creating conda environment with snap and cwltool" >> cdab.stderr
    CONDA_DIR="/opt/anaconda"
    CONDA_PREFIX="${PWD}/env_snap"
    sudo $CONDA_DIR/bin/conda create -p ${CONDA_PREFIX} -y snap=7.0.0 cwltool=3.0.20201203173111 >> cdab.stderr 2>&1
    sudo ln -s "${PWD}/env_snap" "${CONDA_DIR}/envs/env_snap"
    export PATH="${CONDA_DIR}/bin:${CONDA_PREFIX}/bin:${CONDA_PREFIX}/snap/bin:${CONDA_PREFIX}/snap/jre/bin:$PATH"
    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done (conda environment)" >> cdab.stderr

    mkdir -p input_data
    mkdir -p output_data
}


function select_input() {
    # If no inputs are given, select random files from a week to two weeks ago
    if [ -s "$PWD/input" ]
    then
        . $PWD/input
    else
        event_date="2020-08-18T00:03:48Z"
        lon=124.127
        lat=12.026
    fi

    event_date_sec=$(date -d "${event_date}" +%s)
    post_end_date=$(date -d@$((event_date_sec + 10 * 24 * 60 * 60)) +%Y-"%m-%dT00:00:00Z")
    pre_start_date=$(date -d@$((event_date_sec - 10 * 24 * 60 * 60)) +%Y-"%m-%dT00:00:00Z")

    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Selecting post-event product from catalogue" >> cdab.stderr

    if [ "$provider" == "WEKEO" ]
    then
        /opt/anaconda/envs/env_snap/bin/python wekeo-tool.py query --credentials="$credentials" --pn=Sentinel-1 --pt=SLC --geom="POINT($lon $lat)" --dates="${event_date}/${post_end_date}" --count=1 > urls.list 2>> cdab.stderr
        post_id=$(head -1 urls.list | sed -E "s/.*(S1[AB][A-Z0-9_]+).*/\1/g")
    else
        echo "opensearch-client -m Scihub -p \"geom=POINT($lon $lat)\" -p \"start=$event_date\" -p \"stop=$post_end_date\" -p \"pt=SLC\" -p \"count=1\" \"${catalogue_base_url}\" {}" >> cdab.stderr
        opensearch-client $cat_creds -m Scihub -p "geom=POINT($lon $lat)" -p "start=$event_date" -p "stop=$post_end_date" -p "pt=SLC" -p "count=1" "${catalogue_base_url}" {} | xmllint --format - > result.post.atom.xml
        post_id=$(grep "<dc:identifier>" result.post.atom.xml | sed -E "s#.*<.*?>(.*)<.*>.*#\1#g")
        mv result.post.atom.xml ${post_id}.atom.xml
    fi
    track=$(get_track $post_id)
    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done (post-event product = $post_id, track = $track)" >> cdab.stderr

    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Selecting pre-event product from catalogue" >> cdab.stderr

    if [ "$provider" == "WEKEO" ]
    then
        /opt/anaconda/envs/env_snap/bin/python wekeo-tool.py query --credentials="$credentials" --pn=Sentinel-1 --pt=SLC --geom="POINT($lon $lat)" --dates="${pre_start_date}/${event_date}" --count=10 > urls.list 2>> cdab.stderr
        for url in $(cat urls.list)
        do
            pre_id=$(echo $url | sed -E "s/.*(S1[AB][A-Z0-9_]+).*/\1/g")
            track_new=$(get_track $pre_id)
            [ $track_new -eq $track ] && break
        done
    else
        echo "opensearch-client -m Scihub -p \"geom=POINT($lon $lat)\" -p \"start=$pre_start_date\" -p \"stop=$event_date\" -p \"pt=SLC\" -p \"track=$track\" -p \"count=1\" \"${catalogue_base_url}\" {}" >> cdab.stderr
        opensearch-client $cat_creds -m Scihub -p "geom=POINT($lon $lat)" -p "start=$pre_start_date" -p "stop=$event_date" -p "pt=SLC" -p "track=$track" -p "count=1" "${catalogue_base_url}" {} | xmllint --format - > result.pre.atom.xml
        pre_id=$(grep "<dc:identifier>" result.pre.atom.xml | sed -E "s#.*<.*?>(.*)<.*>.*#\1#g")
        mv result.pre.atom.xml ${pre_id}.atom.xml
    fi
    track_new=$(get_track $pre_id)
    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done (pre-event product = $pre_id, track = $track_new)" >> cdab.stderr
    
    if [ $track_new -ne $track ]
    then
        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Pre- and post-event tracks different" >> cdab.stderr
        return 1
    fi
}


function process_interferogram() {

    if [ -z "$pre_id" ] || [ -z "$post_id" ] || [ "$pre_id" == "$post_id" ]
    then
        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Pre- and post-event reference cannot be empty and must be different" >> cdab.stderr
        return
    fi

    end_time=$(($(date +%s) + 4 * 60 * 60))   # Timeout after 4 hour

    echo $pre_id > if-ids.list
    echo $post_id >> if-ids.list

    /opt/anaconda/envs/env_snap/bin/python get-poeorb.py /opt/anaconda/envs/env_snap $pre_id $post_id

    while [ $(date +%s) -lt $end_time ]
    do
        download "if-ids.list" 2 true
        missing=$?

        if [ $missing -eq 0 ]
        then
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Both pre- and post-event product downloaded" >> cdab.stderr
            break
        else
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Wait 5 minutes and retry" >> cdab.stderr
            sleep 300   # wait 5 minutes
        fi
    done

    if [ $missing -ne 0 ]
    then
        wrong_processings=1
        return 1
    fi

    cat > insar.yml << EOF
snap_graph: {class: File, path: ./insar.xml}
pre_event: { class: Directory, path: file://$(find ${PWD}/input_data -type d -name "${pre_id}.SAFE") }
post_event: { class: Directory, path: file://$(find ${PWD}/input_data -type d -name "${post_id}.SAFE") }
EOF

    cat > insar.xml << EOF
<graph id="Graph">
  <version>1.0</version>
  <node id="Read">
    <operator>Read</operator>
    <sources/>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <file>\${pre_event}</file>
      <formatName>SENTINEL-1</formatName>
    </parameters>
  </node>
  <node id="Read(2)">
    <operator>Read</operator>
    <sources/>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <file>\${post_event}</file>
      <formatName>SENTINEL-1</formatName>
    </parameters>
  </node>
  <node id="TOPSAR-Split">
    <operator>TOPSAR-Split</operator>
    <sources>
      <sourceProduct refid="Apply-Orbit-File"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <subswath>IW1</subswath>
      <selectedPolarisations>VV</selectedPolarisations>
      <firstBurstIndex>1</firstBurstIndex>
      <lastBurstIndex>9999</lastBurstIndex>
      <wktAoi/>
    </parameters>
  </node>
  <node id="TOPSAR-Split(2)">
    <operator>TOPSAR-Split</operator>
    <sources>
      <sourceProduct refid="Apply-Orbit-File(2)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <subswath>IW1</subswath>
      <selectedPolarisations>VV</selectedPolarisations>
      <firstBurstIndex>1</firstBurstIndex>
      <lastBurstIndex>9999</lastBurstIndex>
      <wktAoi/>
    </parameters>
  </node>
  <node id="Apply-Orbit-File">
    <operator>Apply-Orbit-File</operator>
    <sources>
      <sourceProduct refid="Read"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <orbitType>Sentinel Precise (Auto Download)</orbitType>
      <polyDegree>3</polyDegree>
      <continueOnFail>false</continueOnFail>
    </parameters>
  </node>
  <node id="Apply-Orbit-File(2)">
    <operator>Apply-Orbit-File</operator>
    <sources>
      <sourceProduct refid="Read(2)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <orbitType>Sentinel Precise (Auto Download)</orbitType>
      <polyDegree>3</polyDegree>
      <continueOnFail>false</continueOnFail>
    </parameters>
  </node>
  <node id="Back-Geocoding">
    <operator>Back-Geocoding</operator>
    <sources>
      <sourceProduct refid="TOPSAR-Split"/>
      <sourceProduct.1 refid="TOPSAR-Split(2)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <demName>SRTM 1Sec HGT</demName>
      <demResamplingMethod>BILINEAR_INTERPOLATION</demResamplingMethod>
      <externalDEMFile/>
      <externalDEMNoDataValue>0.0</externalDEMNoDataValue>
      <resamplingType>BILINEAR_INTERPOLATION</resamplingType>
      <maskOutAreaWithoutElevation>true</maskOutAreaWithoutElevation>
      <outputRangeAzimuthOffset>false</outputRangeAzimuthOffset>
      <outputDerampDemodPhase>false</outputDerampDemodPhase>
      <disableReramp>false</disableReramp>
    </parameters>
  </node>
  <node id="Interferogram">
    <operator>Interferogram</operator>
    <sources>
      <sourceProduct refid="Back-Geocoding"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <subtractFlatEarthPhase>true</subtractFlatEarthPhase>
      <srpPolynomialDegree>5</srpPolynomialDegree>
      <srpNumberPoints>501</srpNumberPoints>
      <orbitDegree>3</orbitDegree>
      <includeCoherence>true</includeCoherence>
      <cohWinAz>2</cohWinAz>
      <cohWinRg>10</cohWinRg>
      <squarePixel>true</squarePixel>
      <subtractTopographicPhase>false</subtractTopographicPhase>
      <demName/>
      <externalDEMFile/>
      <externalDEMNoDataValue>0.0</externalDEMNoDataValue>
      <externalDEMApplyEGM/>
      <tileExtensionPercent/>
      <outputElevation>false</outputElevation>
      <outputLatLon>false</outputLatLon>
    </parameters>
  </node>
  <node id="TOPSAR-Split(3)">
    <operator>TOPSAR-Split</operator>
    <sources>
      <sourceProduct refid="Apply-Orbit-File"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <subswath>IW2</subswath>
      <selectedPolarisations>VV</selectedPolarisations>
      <firstBurstIndex>1</firstBurstIndex>
      <lastBurstIndex>9999</lastBurstIndex>
      <wktAoi/>
    </parameters>
  </node>
  <node id="TOPSAR-Split(4)">
    <operator>TOPSAR-Split</operator>
    <sources>
      <sourceProduct refid="Apply-Orbit-File(2)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <subswath>IW2</subswath>
      <selectedPolarisations>VV</selectedPolarisations>
      <firstBurstIndex>1</firstBurstIndex>
      <lastBurstIndex>9999</lastBurstIndex>
      <wktAoi/>
    </parameters>
  </node>
  <node id="Back-Geocoding(2)">
    <operator>Back-Geocoding</operator>
    <sources>
      <sourceProduct refid="TOPSAR-Split(3)"/>
      <sourceProduct.1 refid="TOPSAR-Split(4)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <demName>SRTM 1Sec HGT</demName>
      <demResamplingMethod>BILINEAR_INTERPOLATION</demResamplingMethod>
      <externalDEMFile/>
      <externalDEMNoDataValue>0.0</externalDEMNoDataValue>
      <resamplingType>BILINEAR_INTERPOLATION</resamplingType>
      <maskOutAreaWithoutElevation>true</maskOutAreaWithoutElevation>
      <outputRangeAzimuthOffset>false</outputRangeAzimuthOffset>
      <outputDerampDemodPhase>false</outputDerampDemodPhase>
      <disableReramp>false</disableReramp>
    </parameters>
  </node>
  <node id="Interferogram(2)">
    <operator>Interferogram</operator>
    <sources>
      <sourceProduct refid="Back-Geocoding(2)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <subtractFlatEarthPhase>true</subtractFlatEarthPhase>
      <srpPolynomialDegree>5</srpPolynomialDegree>
      <srpNumberPoints>501</srpNumberPoints>
      <orbitDegree>3</orbitDegree>
      <includeCoherence>true</includeCoherence>
      <cohWinAz>2</cohWinAz>
      <cohWinRg>10</cohWinRg>
      <squarePixel>true</squarePixel>
      <subtractTopographicPhase>false</subtractTopographicPhase>
      <demName/>
      <externalDEMFile/>
      <externalDEMNoDataValue>0.0</externalDEMNoDataValue>
      <externalDEMApplyEGM/>
      <tileExtensionPercent/>
      <outputElevation>false</outputElevation>
      <outputLatLon>false</outputLatLon>
    </parameters>
  </node>
  <node id="TOPSAR-Split(5)">
    <operator>TOPSAR-Split</operator>
    <sources>
      <sourceProduct refid="Apply-Orbit-File"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <subswath>IW3</subswath>
      <selectedPolarisations>VV</selectedPolarisations>
      <firstBurstIndex>1</firstBurstIndex>
      <lastBurstIndex>9999</lastBurstIndex>
      <wktAoi/>
    </parameters>
  </node>
  <node id="TOPSAR-Split(6)">
    <operator>TOPSAR-Split</operator>
    <sources>
      <sourceProduct refid="Apply-Orbit-File(2)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <subswath>IW3</subswath>
      <selectedPolarisations>VV</selectedPolarisations>
      <firstBurstIndex>1</firstBurstIndex>
      <lastBurstIndex>9999</lastBurstIndex>
      <wktAoi/>
    </parameters>
  </node>
  <node id="Back-Geocoding(3)">
    <operator>Back-Geocoding</operator>
    <sources>
      <sourceProduct refid="TOPSAR-Split(5)"/>
      <sourceProduct.1 refid="TOPSAR-Split(6)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <demName>SRTM 1Sec HGT</demName>
      <demResamplingMethod>BILINEAR_INTERPOLATION</demResamplingMethod>
      <externalDEMFile/>
      <externalDEMNoDataValue>0.0</externalDEMNoDataValue>
      <resamplingType>BILINEAR_INTERPOLATION</resamplingType>
      <maskOutAreaWithoutElevation>true</maskOutAreaWithoutElevation>
      <outputRangeAzimuthOffset>false</outputRangeAzimuthOffset>
      <outputDerampDemodPhase>false</outputDerampDemodPhase>
      <disableReramp>false</disableReramp>
    </parameters>
  </node>
  <node id="Interferogram(3)">
    <operator>Interferogram</operator>
    <sources>
      <sourceProduct refid="Back-Geocoding(3)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <subtractFlatEarthPhase>true</subtractFlatEarthPhase>
      <srpPolynomialDegree>5</srpPolynomialDegree>
      <srpNumberPoints>501</srpNumberPoints>
      <orbitDegree>3</orbitDegree>
      <includeCoherence>true</includeCoherence>
      <cohWinAz>2</cohWinAz>
      <cohWinRg>10</cohWinRg>
      <squarePixel>true</squarePixel>
      <subtractTopographicPhase>false</subtractTopographicPhase>
      <demName/>
      <externalDEMFile/>
      <externalDEMNoDataValue>0.0</externalDEMNoDataValue>
      <externalDEMApplyEGM/>
      <tileExtensionPercent/>
      <outputElevation>false</outputElevation>
      <outputLatLon>false</outputLatLon>
    </parameters>
  </node>
  <node id="TOPSAR-Deburst">
    <operator>TOPSAR-Deburst</operator>
    <sources>
      <sourceProduct refid="Interferogram"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <selectedPolarisations/>
    </parameters>
  </node>
  <node id="TOPSAR-Deburst(2)">
    <operator>TOPSAR-Deburst</operator>
    <sources>
      <sourceProduct refid="Interferogram(2)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <selectedPolarisations/>
    </parameters>
  </node>
  <node id="TOPSAR-Deburst(3)">
    <operator>TOPSAR-Deburst</operator>
    <sources>
      <sourceProduct refid="Interferogram(3)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <selectedPolarisations/>
    </parameters>
  </node>
  <node id="TOPSAR-Merge">
    <operator>TOPSAR-Merge</operator>
    <sources>
      <sourceProduct refid="TOPSAR-Deburst"/>
      <sourceProduct.1 refid="TOPSAR-Deburst(2)"/>
      <sourceProduct.2 refid="TOPSAR-Deburst(3)"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <selectedPolarisations/>
    </parameters>
  </node>
  <node id="Write">
    <operator>Write</operator>
    <sources>
      <sourceProduct refid="TOPSAR-Merge"/>
    </sources>
    <parameters class="com.bc.ceres.binding.dom.XppDomElement">
      <file>./output_data/target.dim</file>
      <formatName>BEAM-DIMAP</formatName>
    </parameters>
  </node>
  <applicationData id="Presentation">
    <Description/>
    <node id="Read">
      <displayPosition x="9.0" y="68.0"/>
    </node>
    <node id="Read(2)">
      <displayPosition x="9.0" y="192.0"/>
    </node>
    <node id="TOPSAR-Split">
      <displayPosition x="125.0" y="6.0"/>
    </node>
    <node id="TOPSAR-Split(2)">
      <displayPosition x="129.0" y="200.0"/>
    </node>
    <node id="Apply-Orbit-File">
      <displayPosition x="10.0" y="102.0"/>
    </node>
    <node id="Apply-Orbit-File(2)">
      <displayPosition x="8.0" y="161.0"/>
    </node>
    <node id="Back-Geocoding">
      <displayPosition x="276.0" y="8.0"/>
    </node>
    <node id="Interferogram">
      <displayPosition x="403.0" y="7.0"/>
    </node>
    <node id="TOPSAR-Split(3)">
      <displayPosition x="123.0" y="36.0"/>
    </node>
    <node id="TOPSAR-Split(4)">
      <displayPosition x="128.0" y="232.0"/>
    </node>
    <node id="Back-Geocoding(2)">
      <displayPosition x="276.0" y="137.0"/>
    </node>
    <node id="Interferogram(2)">
      <displayPosition x="402.0" y="135.0"/>
    </node>
    <node id="TOPSAR-Split(5)">
      <displayPosition x="124.0" y="70.0"/>
    </node>
    <node id="TOPSAR-Split(6)">
      <displayPosition x="128.0" y="262.0"/>
    </node>
    <node id="Back-Geocoding(3)">
      <displayPosition x="265.0" y="254.0"/>
    </node>
    <node id="Interferogram(3)">
      <displayPosition x="396.0" y="253.0"/>
    </node>
    <node id="TOPSAR-Deburst">
      <displayPosition x="513.0" y="10.0"/>
    </node>
    <node id="TOPSAR-Deburst(2)">
      <displayPosition x="519.0" y="135.0"/>
    </node>
    <node id="TOPSAR-Deburst(3)">
      <displayPosition x="511.0" y="252.0"/>
    </node>
    <node id="TOPSAR-Merge">
      <displayPosition x="649.0" y="137.0"/>
    </node>
    <node id="Write">
      <displayPosition x="758.0" y="139.0"/>
    </node>
  </applicationData>
</graph>
EOF


    # Execute graph via cwltool
    ((total_processings++))
    
    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - cwltool --no-container --no-read-only workflow.cwl insar.yml" >> cdab.stderr
    cwltool --no-container --no-read-only workflow.cwl insar.yml >> cdab.stdout 2>> cdab.stderr
    res=$?
    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - EXIT CODE = $res" >> cdab.stderr

    if [ $res -ne 0 ]
    then
        ((wrong_processings++))
        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Processing of $id FAILED" >> cdab.stderr
        return 1
    else
        dim_file=$(find $PWD -name target.dim)
        if [ -z "$dim_file" ]
        then
            echo "No target.dim found" >> cdab.stderr
            return 1
        fi
        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Processing of $id SUCCEEDED" >> cdab.stderr

        output_base_dir=$(dirname $dim_file)

        echo "Result" >> cdab.stderr
        tree --charset ascii $output_base_dir >> cdab.stderr
        find $output_base_dir -type f >> cdab.stderr

        count=0
        errors=0
        for f in $(find . -name "*.img" | grep output_data | grep -v tie_point_grids)
        do 
            size=$(stat -c %s $f)
            [ $size -lt 3000000000 ] && ((errors++))
            ((count++))
        done
        [ $count -lt 3 ] && ((errors++))

        if [ ${errors} -ne 0 ] || [ ${count} -eq 0 ]
        then
            ((wrong_processings++))
        fi

        rm -rf $output_base_dir
    fi
}


function process_stack() {

    max_stack_size=4
    count=0
    empty=0
    printf "" > stack-ids.list

    while [ $count -lt $max_stack_size ]
    do
        ((count++))
        select_start_date=$(date -d@$((event_date_sec - count * 100 * 24 * 60 * 60)) +%Y-"%m-%dT00:00:00Z")
        select_end_date=$(date -d@$((event_date_sec - (count * 100 - 50) * 24 * 60 * 60)) +%Y-"%m-%dT00:00:00Z")
        
        
        atom_file="result.stackitem.atom.xml"
        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Selecting stack product ${count}/${max_stack_size} from catalogue" >> cdab.stderr
        
        if [ "$provider" == "WEKEO" ]
        then
            /opt/anaconda/envs/env_snap/bin/python wekeo-tool.py query --credentials="$credentials" --pn=Sentinel-1 --pt=SLC --geom="POINT($lon $lat)" --dates="${select_start_date}/${select_end_date}" --count=10 > urls.list 2>> cdab.stderr
            for url in $(cat urls.list)
            do
                product_id=$(echo $url | sed -E "s/.*(S1[AB][A-Z0-9_]+).*/\1/g")
                track_new=$(get_track $product_id)
                [ $track_new -eq $track ] && break
            done
            [ $track_new -ne $track ] && product_id=
        else
            echo "opensearch-client -m Scihub -p \"geom=POINT($lon $lat)\" -p \"start=$select_start_date\" -p \"stop=$select_end_date\" -p \"pt=SLC\" -p \"track=$track\" -p \"count=1\" \"${catalogue_base_url}\" {}" >> cdab.stderr
            opensearch-client $cat_creds -m Scihub -p "geom=POINT($lon $lat)" -p "start=$select_start_date" -p "stop=$select_end_date" -p "pt=SLC" -p "track=$track" -p "count=1" "${catalogue_base_url}" {} | xmllint --format - > $atom_file

            product_id=$(grep "<dc:identifier>" ${atom_file} | sed -E "s#.*<.*?>(.*)<.*>.*#\1#g")
            mv $atom_file "${product_id}.atom.xml"
        fi

        if [ -z "$product_id" ]
        then
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Empty result" >> cdab.stderr
            ((wrong++))
            continue
        fi

        track_new=$(get_track $product_id)
        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done (stack product #$count = $product_id, track = $track_new)" >> cdab.stderr
        
        if [ $track_new -ne $track ]
        then
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Track does not match" >> cdab.stderr
            ((wrong++))
            continue
        fi
        
        echo $product_id >> stack-ids.list
    done

    stack_size=$(cat stack-ids.list | wc -l)

    end_time=$(($(date +%s) + 30 * 60))   # Timeout after 30 minutes

    while [ $(date +%s) -lt $end_time ]
    do
        download "stack-ids.list" $stack_size true
        missing=$?

        if [ $missing -eq 0 ]
        then
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - All products downloaded" >> cdab.stderr
            break
        else
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Wait 5 minutes and retry" >> cdab.stderr
            sleep 300   # wait 5 minutes
        fi
    done

    total_processings=$max_stack_size
    wrong_processings=$((wrong + missing))
}



function download() {
    list_file=$1
    size=$2
    delete=$3

    count=0
    missing=0

    for id in $(cat $list_file)
    do
        ((count++))
        echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Processing product ${count}/${size}: ${id}" >> cdab.stderr
        product_folder=$(find ${PWD}/input_data -type d -name "${id}.SAFE")
        if [ -z "$product_folder" ]
        then
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Downloading product ${count}/${size}" >> cdab.stderr
            if [ "$provider" == "SOBLOO" ]
            then
                sobloo_uid=$(curl "https://sobloo.eu/api/v1/services/search?f=identification.externalId:eq:$id" | sed -E 's/.*"uid":"([^"]*)".*/\1/')
                download_url="https://sobloo.eu/api/v1/services/download/${sobloo_uid}"
                mkdir -p input_data/$id
                apikey=$(echo $credentials | sed -E 's/.*:(.*)/\1/')
                echo "curl -H \"Authorization: Apikey ...\" -o \"${id}.zip\" $download_url" >> cdab.stderr
                curl -H "Authorization: Apikey ${apikey}" -o "${id}.zip" $download_url
                # Set env variable, otherwise failure
                export UNZIP_DISABLE_ZIPBOMB_DETECTION=TRUE
                unzip -d input_data/$id "${id}.zip"
            elif [ "$provider" == "WEKEO" ]
            then
                mkdir -p input_data/$id
                echo "Obtaining download URL from WEkEO for $id" >> cdab.stderr
                /opt/anaconda/envs/env_snap/bin/python wekeo-tool.py query --credentials="$credentials" --pn=Sentinel-1 --pt=SLC --uid=$id > download.url 2>> cdab.stderr
                if [ ! -s download.url ]
                then
                    return 1
                fi
                download_url=$(head -1 download.url)
                echo "Downloading from WEkEO: $download_url" >> cdab.stderr
                /opt/anaconda/envs/env_snap/bin/python wekeo-tool.py download --credentials="$credentials" --url="$download_url" --dest="${id}.zip" 2>> cdab.stderr
                unzip -d input_data/$id "${id}.zip"
                path=$(find ./input_data -type d -name IMG_DATA | grep $id)
                if [ -z "$path" ]
                then
                    res=1
                else
                    res=0
                fi
            else
                atom_file="file:///res/${id}.atom.xml"
                echo "docker run -u root --workdir /res -v ${PWD}:/res -v ${HOME}/config/etc/Stars:/etc/Stars/conf.d -v ${HOME}/config/Stars:/root/.config/Stars \"${stage_in_docker_image}\" Stars copy -v \"${atom_file}\" -r 4 -si ${provider} -o /res/input_data/ --allow-ordering" >> cdab.stderr
                docker run -u root --workdir /res -v ${PWD}:/res -v ${HOME}/config/etc/Stars:/etc/Stars/conf.d -v ${HOME}/config/Stars:/root/.config/Stars "${stage_in_docker_image}" Stars copy -v "${atom_file}" -r 4 -si ${provider} -o /res/input_data/ --allow-ordering >> cdab.stdout 2>> cdab.stderr
                res=$?
                if [ $res -ne 0 ]
                then
                    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Error during download" >> cdab.stderr
                    # MUNDI workaround
                    if [ ${provider} == "MUNDI" ] && [ -s "${PWD}/input_data/${id}/${id}.zip" ]
                    then
                        present=
                        echo "Previous error can be ignored, .zip file is present" >> cdab.stderr
                        sudo chown -R $USER "${PWD}/input_data/${id}"
                        product_folder=$(find ${PWD}/input_data -type d -name "${id}.SAFE")
                        if [ -z "$product_folder" ]
                        then
                            unzip -d "${PWD}/input_data/${id}" "${PWD}/input_data/${id}/${id}.zip" >> cdab.stderr 2>> cdab.stderr
                            present=true
                        fi
                        if [ -z "$present" ]
                        then
                            ((missing++))
                            continue
                        fi
                    fi
                fi
            fi

            product_folder=$(find ${PWD}/input_data -type d -name "${id}.SAFE")
            if [ -z "$product_folder" ]
            then
                order_file=$(find ${PWD}/input_data -type f -name "${id}.order.json")
                if [ -n "$order_file" ]
                then
                    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Order for product exists" >> cdab.stderr
                else
                    echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Stack product data not downloaded correctly" >> cdab.stderr
                fi
                ((missing++))
                continue
            fi

            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done (product $count/$size downloaded)" >> cdab.stderr
            ls -l $product_folder
            find $product_folder -type f
        else
            echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - Done (product $count/$size already downloaded)" >> cdab.stderr
        fi
    done

    for f in $(find input_data -name "*.zip")
    do
        sudo rm $f
    done

    return $missing

}


function get_track() {
    id=$1
    abs_orbit=$((10#${id:49:6}))
    case "${id:0:3}" in
        S1A)
            echo $(((abs_orbit + 102) % 175 + 1))
            ;;
        S1B)
            echo $(((abs_orbit + 148) % 175 + 1))
            ;;
    esac
}




# Read parameters
working_dir="$1"
[ "$working_dir" == "." ] && working_dir=$HOME

# 2nd argument is docker image ID, not used
test_site="$3" # e.g. CREO
provider="$4"
credentials="$5"
cat_creds=""

stage_in_docker_image=terradue/stars:1.3.6

case "$provider" in
    CREO)
        catalogue_base_url="https://finder.creodias.eu/resto/api/collections/Sentinel1/describe.xml"
        ;;
    MUNDI)
        catalogue_base_url="https://mundiwebservices.com/acdc/catalog/proxy/search"
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

# Process inputs

echo "--------------------------------" >> cdab.stderr
echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - TC415: Interferogram" >> cdab.stderr

total_processings=0
wrong_processings=0

start_time_if=$(date +%s%N)

if [ $res -eq 0 ]
then
    process_interferogram
fi

end_time_if=$(date +%s%N)

process_duration_if=$(((end_time_if - start_time_if) / 1000000))

if [ ${total_processings} -eq 0 ]
then
    error_rate_if=100.0
    avg_process_duration_if=-1
else
    r=$(echo "scale=4; (${wrong_processings}*100)/${total_processings}+0.04999" | bc)
    error_rate_if=$(printf "%.1f" $r)
    correct_processings=$((total_processings - wrong_processings))
    if [ ${correct_processings} -eq 0 ]
    then
        avg_process_duration_if=-1
    else
        avg_process_duration_if=$((process_duration_if / correct_processings))
    fi
fi
total_processings_if=$total_processings


# Download stack
echo "--------------------------------" >> cdab.stderr
echo "$(date +%Y-"%m-%dT%H:%M:%SZ") - TC416: Stack" >> cdab.stderr

total_processings=0
wrong_processings=0

start_time_st=$(date +%s%N)

if [ $res -eq 0 ]
then
    process_stack
fi

end_time_st=$(date +%s%N)

process_duration_st=$(((end_time_st - start_time_st) / 1000000))



if [ ${total_processings} -eq 0 ]
then
    error_rate_st=100.0
    avg_process_duration_st=-1
else
    r=$(echo "scale=4; (${wrong_processings}*100)/${total_processings}+0.04999" | bc)
    error_rate_st=$(printf "%.1f" $r)
    correct_processings=$((total_processings - wrong_processings))
    if [ ${correct_processings} -eq 0 ]
    then
        avg_process_duration_st=-1
    else
        avg_process_duration_st=$((process_duration_st / correct_processings))
    fi
fi
total_processings_st=$total_processings



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
      "startedAt": "$(date -d @${start_time_if::-9} +%Y-"%m-%dT%H:%M:%SZ")",
      "endedAt": "$(date -d @${end_time_if::-9} +%Y-"%m-%dT%H:%M:%SZ")",
      "duration": ${process_duration_if},
      "metrics": [
        { 
          "name": "errorRate",
          "value": ${error_rate_if},
          "uom": "%"
        },
        { 
          "name": "processDuration",
          "value": ${process_duration_if},
          "uom": "ms"
        },
        { 
          "name": "avgProcessDuration",
          "value": ${avg_process_duration_if},
          "uom": "ms"
        },
        { 
          "name": "processCount",
          "value": ${total_processings_if},
          "uom": "#"
        }
      ]
    },
    {
      "testName": "TC416",
      "className": "cdabtesttools.TestCases.TestCase416",
      "startedAt": "$(date -d @${start_time_st::-9} +%Y-"%m-%dT%H:%M:%SZ")",
      "endedAt": "$(date -d @${end_time_st::-9} +%Y-"%m-%dT%H:%M:%SZ")",
      "duration": ${process_duration_st},
      "metrics": [
        { 
          "name": "errorRate",
          "value": ${error_rate_st},
          "uom": "%"
        },
        { 
          "name": "processDuration",
          "value": ${process_duration_st},
          "uom": "ms"
        },
        { 
          "name": "avgProcessDuration",
          "value": ${avg_process_duration_st},
          "uom": "ms"
        },
        { 
          "name": "processCount",
          "value": ${total_processings_st},
          "uom": "#"
        }
      ]
    }
  ]
}
EOF
