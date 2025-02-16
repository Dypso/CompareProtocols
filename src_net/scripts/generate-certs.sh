#!/bin/bash

# Create directories for certs
mkdir -p ../certs
cd ../certs

# Generate CA key and certificate
openssl genrsa -out ca.key 4096
openssl req -new -x509 -days 3650 -key ca.key -out ca.crt -subj "/C=FR/ST=IDF/L=Paris/O=Billettique/CN=BillettiqueCA"

# Generate server key and CSR
openssl genrsa -out server.key 2048
openssl req -new -key server.key -out server.csr -subj "/C=FR/ST=IDF/L=Paris/O=Billettique/CN=mqtt.billettique.local"

# Generate server certificate
openssl x509 -req -days 365 -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out server.crt

# Generate client key and CSR
openssl genrsa -out client.key 2048
openssl req -new -key client.key -out client.csr -subj "/C=FR/ST=IDF/L=Paris/O=Billettique/CN=client.billettique.local"

# Generate client certificate
openssl x509 -req -days 365 -in client.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out client.crt

# Create password file for mosquitto
echo "admin:$(openssl passwd -6 admin123!)" > ../config/mosquitto/passwd

# Set proper permissions
chmod 644 ca.crt server.crt client.crt
chmod 600 ca.key server.key client.key

# Create config directory if it doesn't exist
mkdir -p ../config/mosquitto

# Clean up
rm *.csr
rm *.srl

echo "Certificates generated successfully in ../certs directory"