#!/bin/bash

# Build script for Linux deployment
# Output directory: Publish_Linux

echo "Starting build for Linux..."

# Define output directories
OUTPUT_DIR="Publish_Linux"
MANAGEMENT_OUT="$OUTPUT_DIR/Management"
SERVER_OUT="$OUTPUT_DIR/Server"

# Clean previous builds
if [ -d "$OUTPUT_DIR" ]; then
    echo "Cleaning output directory..."
    rm -rf "$OUTPUT_DIR"
fi

mkdir -p "$MANAGEMENT_OUT"
mkdir -p "$SERVER_OUT"

# Build Management Project
echo "Building OCPP.Core.Management..."
dotnet publish "OCPP.Core.Management/OCPP.Core.Management.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o "$MANAGEMENT_OUT"

if [ $? -ne 0 ]; then
    echo "Error building Management project!"
    exit 1
fi

# Build Server Project
echo "Building OCPP.Core.Server..."
dotnet publish "OCPP.Core.Server/OCPP.Core.Server.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -o "$SERVER_OUT"

if [ $? -ne 0 ]; then
    echo "Error building Server project!"
    exit 1
fi

echo "Build completed successfully!"
echo "Output is located in $OUTPUT_DIR"
