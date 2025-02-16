param(
    [int]$Duration = 30,
    [int]$EquipmentCount = 5,
    [string]$Protocol = "mqtt"
)

Write-Host "Starting load test with protocol: $Protocol"
Write-Host "Duration: $Duration seconds"
Write-Host "Equipment count: $EquipmentCount"

# Démarrage progressif des équipements
$batchSize = 1
for ($i = 0; $i -lt $EquipmentCount; $i += $batchSize) {
    $count = [Math]::Min($batchSize, $EquipmentCount - $i)
    Write-Host "Starting batch of $count equipments..."
    
    Start-Process -FilePath "dotnet" -ArgumentList "run --project src_net/Equipment.Simulator/Equipment.Simulator.csproj -- --protocol $Protocol --start-id $i --count $count" -NoNewWindow
    Start-Sleep -Seconds 5
}

Write-Host "All equipment simulators started. Test will run for $Duration seconds."
Start-Sleep -Seconds $Duration

Write-Host "Test completed. Stopping simulators..."
Get-Process -Name "Equipment.Simulator" | Stop-Process -Force

# Analyse des résultats
Write-Host "Analyzing results..."
try {
    $metrics = Invoke-RestMethod -Uri "http://localhost:9090/api/v1/query?query=validations_received_total"
    $totalValidations = $metrics.data.result[0].value[1]

    $latencyMetrics = Invoke-RestMethod -Uri "http://localhost:9090/api/v1/query?query=histogram_quantile(0.95,rate(validation_latency_seconds_bucket[5m]))"
    $p95Latency = $latencyMetrics.data.result[0].value[1]

    Write-Host "Results:"
    Write-Host "Total validations processed: $totalValidations"
    Write-Host "P95 Latency: $p95Latency seconds"
}
catch {
    Write-Host "Error retrieving metrics: $_"
}