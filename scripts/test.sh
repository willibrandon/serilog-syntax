#!/bin/bash
# Test script for SerilogSyntax (WSL/Linux version)
# This script calls the Windows PowerShell script from WSL

# Default values
CONFIGURATION="Debug"
FILTER=""
NO_BUILD=""
ITERATIONS="1"

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    -c|--configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    -f|--filter)
      FILTER="$2"
      shift 2
      ;;
    --no-build)
      NO_BUILD="-NoBuild"
      shift
      ;;
    -i|--iterations)
      ITERATIONS="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [-c|--configuration Debug|Release] [-f|--filter TestName] [--no-build] [-i|--iterations N]"
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
PS_CMD="powershell -ExecutionPolicy Bypass -File scripts\\test.ps1 -Configuration $CONFIGURATION"

if [ -n "$FILTER" ]; then
    PS_CMD="$PS_CMD -Filter \"$FILTER\""
fi

if [ -n "$NO_BUILD" ]; then
    PS_CMD="$PS_CMD $NO_BUILD"
fi

if [ "$ITERATIONS" != "1" ]; then
    PS_CMD="$PS_CMD -Iterations $ITERATIONS"
fi

# Use cmd.exe to call the PowerShell script on Windows
cmd.exe /c "$PS_CMD"