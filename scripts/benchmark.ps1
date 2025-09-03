# Benchmark script for SerilogSyntax
# Runs BenchmarkDotNet performance tests to measure optimization impact

param(
    [string]$Configuration = "Release",  # Build configuration (Debug/Release)
    [string]$Filter = "",                # Filter benchmarks by name
    [switch]$NoBuild,                    # Skip build step
    [switch]$QuickRun                    # Use short job for faster results
)

# Determine the root directory (parent of scripts folder or current directory)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($scriptDir -match "scripts$") {
    $rootDir = Split-Path -Parent $scriptDir
} else {
    $rootDir = $scriptDir
}

# Change to root directory for benchmark
Push-Location $rootDir
try {
    # Build first unless -NoBuild is specified
    if (-not $NoBuild) {
        Write-Host "Building solution in Release mode..." -ForegroundColor Cyan
        $buildScript = Join-Path $scriptDir "build.ps1"
        & powershell -ExecutionPolicy Bypass -File $buildScript -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

# Run benchmarks
Write-Host "`nRunning benchmarks..." -ForegroundColor Cyan
$benchmarkExe = "SerilogSyntax.Benchmarks\bin\$Configuration\net472\SerilogSyntax.Benchmarks.exe"

if (-not (Test-Path $benchmarkExe)) {
    Write-Error "Benchmark executable not found: $benchmarkExe"
    exit 1
}

$args = @()

# Add filter if specified
if ($Filter) {
    $args += "--filter"
    $args += "*$Filter*"
}

# Add quick run option for faster results (less accurate)
if ($QuickRun) {
    $args += "--job"
    $args += "Short"
}

# Run all benchmarks if no filter specified
if (-not $Filter) {
    Write-Host "Running all benchmarks (this may take several minutes)..." -ForegroundColor Yellow
    Write-Host "Use -Filter parameter to run specific benchmarks" -ForegroundColor Gray
    Write-Host "Use -QuickRun for faster but less accurate results" -ForegroundColor Gray
    Write-Host ""
}

# Execute benchmarks
& $benchmarkExe @args

    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nBenchmarks completed successfully!" -ForegroundColor Green
        Write-Host "Results saved to: SerilogSyntax.Benchmarks\BenchmarkDotNet.Artifacts\results\" -ForegroundColor Gray
    } else {
        Write-Host "`nBenchmarks failed!" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} finally {
    Pop-Location
}