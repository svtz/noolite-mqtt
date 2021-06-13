#!/bin/bash
# exit when any command fails
set -e

remove_container () {
    echo -n "Removing $1..."
    containerId=$(docker ps -a -q --filter name=$1)

    if [ -z "$containerId" ]
    then
        echo "No such container, skipped"
    else
        docker kill $containerId > /dev/null
        docker rm $containerId > /dev/null
        echo "OK"
    fi
}

remove_container "noolite_mqtt"

echo -n "Starting noolite_mqtt..."
docker pull svtz/noolite-mqtt:latest > /dev/null
docker run --detach \
    --volume /home/svtz/wqtt/logs/noolite-mqtt:/app/logs \
    --name noolite_mqtt \
    --restart=always \
    --env-file=./env.list \
    --network="host" \
    --privileged \
    --volume /dev/serial:/dev/serial \
    svtz/noolite-mqtt:latest > /dev/null
echo "OK"

echo "COMPLETE"
