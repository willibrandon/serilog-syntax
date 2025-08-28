# Test script for SerilogSyntax

param(
    [string]$Configuration = "Debug",
    [string]$Filter = "",
    [switch]$NoBuild
)

# Find vstest.console.exe
$vstestPaths = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Professional\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe"
)

$vstest = $null
foreach ($path in $vstestPaths) {
    if (Test-Path $path) {
        $vstest = $path
        break
    }
}

if (-not $vstest) {
    Write-Error "vstest.console.exe not found. Please ensure Visual Studio 2022 is installed."
    exit 1
}

# Build first unless -NoBuild is specified
if (-not $NoBuild) {
    Write-Host "Building solution..." -ForegroundColor Cyan
    & powershell -ExecutionPolicy Bypass -File build.ps1 -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

# Run tests
Write-Host "`nRunning tests..." -ForegroundColor Cyan
$testDll = "SerilogSyntax.Tests\bin\$Configuration\net472\SerilogSyntax.Tests.dll"

if (-not (Test-Path $testDll)) {
    Write-Error "Test assembly not found: $testDll"
    exit 1
}

$args = @($testDll, "/Logger:console;verbosity=minimal")
if ($Filter) {
    $args += "/TestCaseFilter:FullyQualifiedName~$Filter"
}

& "$vstest" @args

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nAll tests passed!" -ForegroundColor Green
} else {
    Write-Host "`nTests failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}