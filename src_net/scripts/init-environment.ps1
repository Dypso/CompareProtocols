# Create necessary directories
New-Item -ItemType Directory -Force -Path certs | Out-Null
New-Item -ItemType Directory -Force -Path config/mosquitto | Out-Null

Write-Host "Generating certificates..."

# Set OpenSSL configuration and ensure it's used
$openSslConfigPath = Join-Path $PWD "openssl.cnf"
$openSslConfig = @"
[req]
default_bits = 2048
prompt = no
default_md = sha256
distinguished_name = dn
x509_extensions = v3_ca

[dn]
C = FR
ST = IDF
L = Paris
O = Billettique
CN = BillettiqueCA

[v3_ca]
basicConstraints = critical,CA:TRUE
keyUsage = critical,keyCertSign,cRLSign
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid:always,issuer
"@

Set-Content -Path $openSslConfigPath -Value $openSslConfig -Encoding ASCII

# Ensure the config file exists
if (-not (Test-Path $openSslConfigPath)) {
    Write-Host "Error: Could not create OpenSSL config file"
    exit 1
}

# Set OPENSSL_CONF environment variable
$env:OPENSSL_CONF = $openSslConfigPath

# Generate CA certificate
Write-Host "Generating CA certificate..."
& openssl req -x509 -new -nodes -sha256 -days 365 -newkey rsa:2048 -keyout certs/ca.key -out certs/ca.crt -config $openSslConfigPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error generating CA certificate"
    exit 1
}

# Generate server certificate
Write-Host "Generating server certificate..."
& openssl req -new -nodes -sha256 -newkey rsa:2048 -keyout certs/server.key -out certs/server.csr -config $openSslConfigPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error generating server certificate"
    exit 1
}

# Sign server certificate
Write-Host "Signing server certificate..."
& openssl x509 -req -days 365 -in certs/server.csr -CA certs/ca.crt -CAkey certs/ca.key -CAcreateserial -out certs/server.crt -sha256
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error signing server certificate"
    exit 1
}

# Generate PKCS#12 file for HTTP/2
Write-Host "Generating PKCS#12 file..."
& openssl pkcs12 -export -in certs/server.crt -inkey certs/server.key -out certs/server.pfx -passout pass:cert123!
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error generating PKCS#12 file"
    exit 1
}

# Create Mosquitto password file
Write-Host "Creating Mosquitto password file..."
$passwordFile = "config/mosquitto/passwd"
Set-Content -Path $passwordFile -Value "admin:$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LuYuGPaBV9R1UUJ2K"

Write-Host "Setting file permissions..."
# Set appropriate permissions for Windows
$acl = Get-Acl "certs/ca.key"
$acl.SetAccessRuleProtection($true, $false)
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule("SYSTEM", "FullControl", "Allow")
$acl.AddAccessRule($rule)
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule($env:USERNAME, "FullControl", "Allow")
$acl.AddAccessRule($rule)
Set-Acl "certs/ca.key" $acl
Set-Acl "certs/server.key" $acl
Set-Acl "certs/server.pfx" $acl

Write-Host "Cleaning up temporary files..."
Remove-Item -Path "certs/*.csr" -ErrorAction SilentlyContinue
Remove-Item -Path "certs/*.srl" -ErrorAction SilentlyContinue
Remove-Item -Path $openSslConfigPath -ErrorAction SilentlyContinue

# Remove the environment variable
Remove-Item Env:\OPENSSL_CONF -ErrorAction SilentlyContinue

Write-Host "Environment initialized successfully"