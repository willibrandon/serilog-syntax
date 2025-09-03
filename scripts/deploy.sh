#!/bin/bash
# Deploy script for SerilogSyntax (WSL/Linux version)
# This script calls the Windows PowerShell script from WSL

# Default values
CONFIGURATION="Debug"
RESTART_VS=""
EXPERIMENTAL=""
ALL=""

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    -c|--configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    --restart-vs)
      RESTART_VS="-RestartVS"
      shift
      ;;
    --experimental)
      EXPERIMENTAL="-Experimental"
      shift
      ;;
    --all)
      ALL="-All"
      shift
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [-c|--configuration Debug|Release] [--restart-vs] [--experimental] [--all]"
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

# Build the PowerShell command
PS_CMD="powershell -ExecutionPolicy Bypass -File scripts\\deploy.ps1 -Configuration $CONFIGURATION"

if [ -n "$RESTART_VS" ]; then
    PS_CMD="$PS_CMD $RESTART_VS"
fi

if [ -n "$EXPERIMENTAL" ]; then
    PS_CMD="$PS_CMD $EXPERIMENTAL"
fi

if [ -n "$ALL" ]; then
    PS_CMD="$PS_CMD $ALL"
fi

# Use cmd.exe to call the PowerShell script on Windows
cmd.exe /c "$PS_CMD"