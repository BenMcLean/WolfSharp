#!/bin/bash
# Downloads the Godot OpenXR Vendors plugin for Quest standalone support.
# This plugin is .gitignored and must be downloaded separately.
#
# Usage: ./setup-openxr-vendors.sh
#
# The plugin is MIT licensed, but contains vendor-specific components
# with separate license terms. See: https://github.com/GodotVR/godot_openxr_vendors

set -e

# Plugin version - update this when upgrading
PLUGIN_VERSION="4.3.1-stable"

# Minimum Godot version required for this plugin version
MIN_GODOT_VERSION="4.4"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ADDONS_DIR="$SCRIPT_DIR/addons"
PLUGIN_DIR="$ADDONS_DIR/godotopenxrvendors"

# GitHub release URL
DOWNLOAD_URL="https://github.com/GodotVR/godot_openxr_vendors/releases/download/${PLUGIN_VERSION}/godotopenxrvendorsaddon.zip"
TEMP_ZIP="/tmp/godotopenxrvendorsaddon.zip"

echo "Godot OpenXR Vendors Plugin Setup"
echo "=================================="
echo "Version: $PLUGIN_VERSION"
echo "Requires: Godot $MIN_GODOT_VERSION+"
echo ""

# Check if already installed
if [ -d "$PLUGIN_DIR" ]; then
    echo "Plugin already installed at: $PLUGIN_DIR"
    read -p "Reinstall/update? (y/N) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Aborted."
        exit 0
    fi
    echo "Removing existing installation..."
    rm -rf "$PLUGIN_DIR"
fi

# Create addons directory if needed
mkdir -p "$ADDONS_DIR"

echo "Downloading from GitHub..."
echo "URL: $DOWNLOAD_URL"

# Download using curl or wget
if command -v curl &> /dev/null; then
    curl -L -o "$TEMP_ZIP" "$DOWNLOAD_URL"
elif command -v wget &> /dev/null; then
    wget -O "$TEMP_ZIP" "$DOWNLOAD_URL"
else
    echo "ERROR: Neither curl nor wget found. Please install one of them."
    exit 1
fi

echo "Extracting..."
TEMP_EXTRACT="/tmp/godotopenxrvendors_extract"
rm -rf "$TEMP_EXTRACT"
mkdir -p "$TEMP_EXTRACT"
unzip -q "$TEMP_ZIP" -d "$TEMP_EXTRACT"
rm -f "$TEMP_ZIP"

mkdir -p "$ADDONS_DIR"
if [ -d "$TEMP_EXTRACT/asset/addons/godotopenxrvendors" ]; then
    cp -r "$TEMP_EXTRACT/asset/addons/godotopenxrvendors" "$ADDONS_DIR/"
elif [ -d "$TEMP_EXTRACT/addons/godotopenxrvendors" ]; then
    cp -r "$TEMP_EXTRACT/addons/godotopenxrvendors" "$ADDONS_DIR/"
else
    echo "ERROR: Could not find godotopenxrvendors in extracted zip"
    rm -rf "$TEMP_EXTRACT"
    exit 1
fi
rm -rf "$TEMP_EXTRACT"

# Verify installation
if [ -d "$PLUGIN_DIR" ]; then
    echo ""
    echo "SUCCESS: Plugin installed to $PLUGIN_DIR"
    echo ""
    echo "Note: This plugin is required for Quest standalone builds."
    echo "PC VR (SteamVR, Oculus Link) works without this plugin."
    echo ""
    echo "License: MIT (wrapper code)"
    echo "         Vendor-specific components have separate licenses."
    echo "         See: $PLUGIN_DIR/LICENSE files"
else
    echo "ERROR: Installation failed - plugin directory not found"
    exit 1
fi
