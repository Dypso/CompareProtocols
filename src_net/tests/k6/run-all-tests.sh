#!/bin/bash

echo "Starting gRPC load test..."
k6 run grpc-load-test.js

echo -e "\nStarting HTTP/2 load test..."
sleep 10
k6 run http2-load-test.js

echo -e "\nStarting MQTT load test..."
sleep 10
k6 run mqtt-load-test.js

echo -e "\nAll load tests completed"

# Export results if InfluxDB is configured
# k6 run --out influxdb=http://localhost:8086/k6 grpc-load-test.js
# k6 run --out influxdb=http://localhost:8086/k6 http2-load-test.js
# k6 run --out influxdb=http://localhost:8086/k6 mqtt-load-test.js