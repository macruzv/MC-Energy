#!/bin/bash

# Exit on error
set -e

echo "Cleaning previous builds..."
rm -rf Publish_Linux
mkdir Publish_Linux

echo "Building Management (Web UI)..."
dotnet publish OCPP.Core.Management/OCPP.Core.Management.csproj -c Release -r linux-x64 --self-contained false -o ./Publish_Linux/Management

echo "Building Server (OCPP Endpoint)..."
dotnet publish OCPP.Core.Server/OCPP.Core.Server.csproj -c Release -r linux-x64 --self-contained false -o ./Publish_Linux/Server

echo "Copying Database Files (if needed for seeding)..."
# Optional: Copy default sqlite if used, or ensure appsettings are present
cp OCPP.Core.Management/appsettings.json ./Publish_Linux/Management/
cp OCPP.Core.Server/appsettings.json ./Publish_Linux/Server/

echo "Build Complete! Files are in ./Publish_Linux/"
