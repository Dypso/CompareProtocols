# Start each test with a delay between them
Write-Host "Starting gRPC load test..."
Start-Process -FilePath "k6" -ArgumentList "run", "grpc-load-test.js" -NoNewWindow -Wait

Write-Host "`nStarting HTTP/2 load test..."
Start-Sleep -Seconds 10
Start-Process -FilePath "k6" -ArgumentList "run", "http2-load-test.js" -NoNewWindow -Wait

Write-Host "`nStarting MQTT load test..."
Start-Sleep -Seconds 10
Start-Process -FilePath "k6" -ArgumentList "run", "mqtt-load-test.js" -NoNewWindow -Wait

Write-Host "`nAll load tests completed"

# Export results if InfluxDB is configured
# k6 run --out influxdb=http://localhost:8086/k6 grpc-load-test.js
# k6 run --out influxdb=http://localhost:8086/k6 http2-load-test.js
# k6 run --out influxdb=http://localhost:8086/k6 mqtt-load-test.js