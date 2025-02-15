param(
    [int]$Duration = 30,
    [int]$EquipmentCount = 5,
    [string]$Protocol = "mqtt" # mqtt, http2, grpc
)

Write-Host "Starting load test with protocol: $Protocol"
Write-Host "Duration: $Duration seconds"
Write-Host "Equipment count: $EquipmentCount"

# Démarrage progressif des équipements
$batchSize = 1
for ($i = 0; $i -lt $EquipmentCount; $i += $batchSize) {
    $count = [Math]::Min($batchSize, $EquipmentCount - $i)
    Write-Host "Starting batch of $count equipments..."
    
    Start-Process dotnet -ArgumentList "run --project src_net/Equipment.Simulator/Equipment.Simulator.csproj -- --protocol $Protocol --start-id $i --count $count" -NoNewWindow
    Start-Sleep -Seconds 5
}

Write-Host "All equipment simulators started. Test will run for $Duration seconds."
Start-Sleep -Seconds $Duration

Write-Host "Test completed. Stopping simulators..."
Get-Process -Name "Equipment.Simulator" | Stop-Process -Force]]>