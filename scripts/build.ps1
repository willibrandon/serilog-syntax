# Build script for SerilogSyntax VSIX project

param(
    [string]$Configuration = "Debug",
    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal"
)

# Determine the root directory (parent of scripts folder or current directory)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($scriptDir -match "scripts$") {
    $rootDir = Split-Path -Parent $scriptDir
} else {
    $rootDir = $scriptDir
}

# Change to root directory for build
Push-Location $rootDir
try {

# Common MSBuild paths for VS 2022
$msbuildPaths = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
)

# Find MSBuild
$msbuild = $null
foreach ($path in $msbuildPaths) {
    if (Test-Path $path) {
        $msbuild = $path
        break
    }
}

    if (-not $msbuild) {
        Write-Error "MSBuild not found. Please ensure Visual Studio 2022 is installed."
        exit 1
    }

    Write-Host "Building SerilogSyntax ($Configuration)..." -ForegroundColor Cyan

    & "$msbuild" "SerilogSyntax.sln" `
        /p:Configuration=$Configuration `
        /p:Platform="Any CPU" `
        /restore `
        /verbosity:$Verbosity `
        /nologo `
        /consoleloggerparameters:Summary

    if ($LASTEXITCODE -eq 0) {
        Write-Host "`nBuild succeeded!" -ForegroundColor Green
        Write-Host "Output: SerilogSyntax\bin\$Configuration\SerilogSyntax.vsix"
    } else {
        Write-Host "`nBuild failed!" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} finally {
    Pop-Location
}