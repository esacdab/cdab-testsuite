#!/bin/bash

#
# System activity data collector for specific intervals
#   input: test result file $TEST_RESULT_FILE
#   output: global array var $DD 
function data_collect() {
    local INTERVAL_S="${1}"
    local COUNT="${2}"
    local SA_DATAFILE="${3}"
    $SADC_BIN $INTERVAL_S $COUNT $SADC_OPTIONS $SA_DATAFILE
}

#
# get the test result times (start, end, duration)
#   input: test result file $TEST_RESULT_FILE
#   output: global array var $DD 
#
function get_test_times() {
    local TEST_RESULT_FILE="${1}"
    
    TEST_TS=( $(cat $TEST_RESULT_FILE | python $PYTHON_SCRIPT) )
    for (( i=0;i<2;i++)); do     
        DD_TMP[${i}]=$(echo -n "${TEST_TS[${i}]}" | awk -F[-T] '{print $3}')
        # min_startTime, max_endTime
        TS_START_END[${i}]=$(echo -n "${TEST_TS[${i}]}" | awk -F[T.] '{print $2}')
    done
    # sorted unique values
    DD=($(echo "${DD_TMP[@]}" | tr ' ' '\n' | sort -u | tr '\n' ' '))        
    DUR=$(( ${TEST_TS[2]}/1000 ))
    
    if [ "$DEBUG" = "1" ]; then
        echo "TEST_TS=${TEST_TS[*]}"
        echo "DD=${DD[*]}"
        echo "TS_START_END=${TS_START_END[*]}"
        echo "DUR=${DUR}"
    fi   
}

#
# get the count and duration for sadf
#   input: duration in secs $1
#   output: global vars $COUNT $INTERVAL_S
#
function get_count_interval() {
    local DUR="${1}"
    INTERVAL_S=$(( $DUR/$MAX_COUNT ))
    if [ $INTERVAL_S -eq 0 ]; then 
        INTERVAL_S=1
    fi         
    ARR_INTERVAL_S=( $MAX_INTERVAL_S $INTERVAL_S )
    IFS=$'\n'
    INTERVAL_S=$(echo "${ARR_INTERVAL_S[*]}" | sort -n | head -n1)

    COUNT=$(( $DUR/$INTERVAL_S ))
    ARR_COUNT=( $MAX_COUNT $COUNT )
    COUNT=$(echo "${ARR_COUNT[*]}" | sort -n | head -n1)
   
    if [ "$DEBUG" = "1" ]; then
        echo "INTERVAL_S=${INTERVAL_S}"
        echo "COUNT=${COUNT}"    
    fi
}

#-- Main -------------------------------------
OIFS=$IFS;

LC_ALL=C
PATH=/usr/lib/sysstat:/usr/sbin:/usr/sbin:/usr/bin:/sbin:/bin

OS_TYPE=$(cat /etc/os-release | grep "^ID=" | sed 's|"||g')
if [ "${OS_TYPE}" = "ubuntu" ]; then
    SADC_BIN=/usr/lib/sysstat/sadc
else
    SADC_BIN=/usr/lib64/sa/sadc
fi

# sar options
SAR_OPTIONS="-- -bBdqrSuwWF -n DEV -n EDEV -n IP -n EIP -n ICMP -n EICMP -n TCP -n ETCP -n UDP"
SADC_OPTIONS="-S XDISK"

# By default take info from /var/log/sa/sa$DD
# sar data collection schedule on file /etc/cron.d/sysstat
# default = 10 min, minimum = 1 min
# max sampling : every 10 min, 144 per day
MIN_COUNT=6
MIN_INTERVAL_S=10
MAX_COUNT=144
MAX_INTERVAL_S=600

CDAB_TOOLS=$(dirname $0)
PYTHON_SCRIPT=$CDAB_TOOLS/sadf_in.py

# first argument as input file
if [ ! -f $1 ]; then 
    echo '*** Error: $1 file not found. ***' >&2
    exit 1
else 
    TEST_RESULT_FILE=$1
fi 

echo ">>> Saving system activity data collected by sysstat tool ..."

#-- extract start/end test times
get_test_times $TEST_RESULT_FILE

#-- get count and interval from duration
get_count_interval $DUR

#-- System activity data collection
DD_NOW=$(date "+%d")
if [ ! -f "/var/log/sa/sa${DD_NOW}" ]; then
    # collecting at leat 1 minute starting from now to file /tmp/sa$DD
    SA_DATAFILE="/tmp/sa${DD_NOW}"
    data_collect $MIN_INTERVAL_S $MIN_COUNT $SA_DATAFILE
fi

#-- Display data collected by sar
CNT=1
CNT_DD="${#DD[@]}"
if [ $CNT_DD = "1" ]; then
    VALUES=( ${DD[0]} )
else
    VALUES=( $(seq ${DD[0]} ${DD[1]}) )
fi
CNT_LAST="${#VALUES[@]}"

for i in "${VALUES[@]}"; do
    START="00:00:00"
    END="23:59:59"
    # min_startTime
    if [ $CNT = 1 ]; then
        START=${TS_START_END[0]}
    fi
    # max_endTime
    if [ "$CNT" = "$CNT_LAST" ]; then
        END=${TS_START_END[1]}
    fi
    #echo "-- " $CNT $i $START $END
    SA_DATAFILE="/var/log/sa/sa${i}"
    /bin/sadf -j -s $START -e $END $SA_DATAFILE -- -bBdqrSuwW -n DEV -n EDEV -n IP -n EIP -n ICMP -n EICMP -n TCP -n ETCP -n UDP > "./sysstat${i}.json"
    echo "Saved on ./sysstat${i}.json"
    
    CNT=$(( $CNT + 1 ))
done

echo "Done."

IFS=$OIFS; 
exit
#-- End  ------------------------------------