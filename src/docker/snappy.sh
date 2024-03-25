
#!/bin/bash

/opt/snap/bin/snappy-conf /usr/bin/python3 /usr/local/lib/python3.8/dist-packages/ > /var/log/snappy-conf.log 2>&1 &
tail -f /var/log/snappy-conf.log |
while read LOGLINE
    do 
        echo $LOGLINE; 
        if [[ "${LOGLINE}" == *"Done."* ]]; then 
            echo "Shutting down snappy-conf ($$)"
            exit 0;
        fi
    done