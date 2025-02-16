Write-Host "Starting infrastructure services..."
docker-compose up -d

Write-Host "Waiting for services to be ready..."
Start-Sleep -Seconds 30

Write-Host "Starting gRPC service..."
Start-Process -FilePath "dotnet" -ArgumentList "run --project src_net/Grpc.Solution/Grpc.Solution.csproj" -NoNewWindow
Start-Sleep -Seconds 5

Write-Host "Starting HTTP/2 service..."
Start-Process -FilePath "dotnet" -ArgumentList "run --project src_net/Http2.Solution/Http2.Solution.csproj" -NoNewWindow
Start-Sleep -Seconds 5

Write-Host "Starting MQTT service..."
Start-Process -FilePath "dotnet" -ArgumentList "run --project src_net/Mqtt.Solution/Mqtt.Solution.csproj" -NoNewWindow
Start-Sleep -Seconds 5

Write-Host "All services started. Press Ctrl+C to stop."

# Attendre indéfiniment
while ($true) {
    Start-Sleep -Seconds 1
}