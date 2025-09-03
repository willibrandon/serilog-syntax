#!/bin/bash
# Benchmark script for SerilogSyntax (WSL/Linux version)
# This script calls the Windows PowerShell script from WSL

# Default values
CONFIGURATION="Release"
FILTER=""
NO_BUILD=""
QUICK_RUN=""

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
    --quick)
      QUICK_RUN="-QuickRun"
      shift
      ;;
    *)
      echo "Unknown option: $1"
      echo "Usage: $0 [-c|--configuration Debug|Release] [-f|--filter BenchmarkName] [--no-build] [--quick]"
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
PS_CMD="powershell -ExecutionPolicy Bypass -File scripts\\benchmark.ps1 -Configuration $CONFIGURATION"

if [ -n "$FILTER" ]; then
    PS_CMD="$PS_CMD -Filter \"$FILTER\""
fi

if [ -n "$NO_BUILD" ]; then
    PS_CMD="$PS_CMD $NO_BUILD"
fi

if [ -n "$QUICK_RUN" ]; then
    PS_CMD="$PS_CMD $QUICK_RUN"
fi

# Use cmd.exe to call the PowerShell script on Windows
cmd.exe /c "$PS_CMD"