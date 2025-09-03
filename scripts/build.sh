#!/bin/bash
# Build script for SerilogSyntax VSIX project (WSL/Linux version)
# This script calls the Windows PowerShell script from WSL

# Default values
CONFIGURATION="Debug"

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    -c|--configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [-c|--configuration Debug|Release]"
      exit 1
      ;;
  esac
done

# Determine the script directory and root directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ "$SCRIPT_DIR" == */scripts ]]; then
    ROOT_DIR="$(dirname "$SCRIPT_DIR")"
else
    ROOT_DIR="$SCRIPT_DIR"
fi

# Change to root directory
cd "$ROOT_DIR"

# Use cmd.exe to call the PowerShell script on Windows
cmd.exe /c "powershell -ExecutionPolicy Bypass -File scripts\\build.ps1 -Configuration $CONFIGURATION"