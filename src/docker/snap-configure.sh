
#!/bin/bash

/opt/snap/bin/snappy-conf /usr/bin/python3 /usr/local/lib/python3.8/dist-packages/ > /var/log/snappy-conf.log 2>&1 &
tail -f /var/log/snappy-conf.log |
while read LOGLINE
    do 
        echo $LOGLINE; 
        if [[ "${LOGLINE}" == *"Done."* ]]; then 
            echo "Shutting down snappy-conf ($$)"
            break;
        fi
    done

snap --nosplash --nogui --modules --install org.esa.snap.idepix.core > /var/log/idepix-conf.log 2>&1 &
tail -f /var/log/idepix-conf.log|
while read LOGLINE
    do 
        echo $LOGLINE; 
        if [[ "${LOGLINE}" == *"No match for"* ]]; then 
            echo "Shutting down install ($$)"
            break;
        fi
    done

snap --nosplash --nogui --modules --install org.esa.snap.idepix.olci > /var/log/olci-conf.log 2>&1 &
tail -f /var/log/olci-conf.log|
while read LOGLINE
    do 
        echo $LOGLINE; 
        if [[ "${LOGLINE}" == *"No match for"* ]]; then 
            echo "Shutting down install ($$)"
            exit 0;
        fi
    done