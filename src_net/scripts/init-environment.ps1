# Create necessary directories
New-Item -ItemType Directory -Force -Path certs | Out-Null
New-Item -ItemType Directory -Force -Path config/mosquitto | Out-Null

Write-Host "Generating certificates..."

# Generate CA certificate
$caSubject = "/C=FR/ST=IDF/L=Paris/O=Billettique/CN=BillettiqueCA"
openssl req -new -x509 -days 365 -nodes `
    -out certs/ca.crt `
    -keyout certs/ca.key `
    -subj $caSubject

# Generate server certificate
$serverSubject = "/C=FR/ST=IDF/L=Paris/O=Billettique/CN=localhost"
openssl req -new -nodes `
    -out certs/server.csr `
    -keyout certs/server.key `
    -subj $serverSubject

# Sign server certificate
openssl x509 -req -days 365 `
    -in certs/server.csr `
    -CA certs/ca.crt `
    -CAkey certs/ca.key `
    -CAcreateserial `
    -out certs/server.crt

# Generate PKCS#12 file for HTTP/2
openssl pkcs12 -export `
    -in certs/server.crt `
    -inkey certs/server.key `
    -out certs/server.pfx `
    -passout pass:cert123!

# Create Mosquitto password file
$passwordFile = "config/mosquitto/passwd"
Set-Content -Path $passwordFile -Value "admin:$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LuYuGPaBV9R1UUJ2K"

Write-Host "Setting file permissions..."
# Note: Windows equivalent of chmod
$acl = Get-Acl "certs/ca.key"
$acl.SetAccessRuleProtection($true, $false)
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule("SYSTEM","FullControl","Allow")
$acl.AddAccessRule($rule)
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule($env:USERNAME,"FullControl","Allow")
$acl.AddAccessRule($rule)
Set-Acl "certs/ca.key" $acl
Set-Acl "certs/server.key" $acl
Set-Acl "certs/server.pfx" $acl

Write-Host "Cleaning up temporary files..."
Remove-Item -Path "certs/*.csr" -ErrorAction SilentlyContinue
Remove-Item -Path "certs/*.srl" -ErrorAction SilentlyContinue

Write-Host "Environment initialized successfully"