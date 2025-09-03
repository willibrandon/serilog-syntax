# Test script for SerilogSyntax

param(
    [string]$Configuration = "Debug",
    [string]$Filter = "",
    [switch]$NoBuild,
    [int]$Iterations = 1
)

# Determine the root directory (parent of scripts folder or current directory)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($scriptDir -match "scripts$") {
    $rootDir = Split-Path -Parent $scriptDir
} else {
    $rootDir = $scriptDir
}

# Change to root directory for test
Push-Location $rootDir
try {
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
        $buildScript = Join-Path $scriptDir "build.ps1"
        & powershell -ExecutionPolicy Bypass -File $buildScript -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    # Run tests (potentially multiple iterations)
    $testDll = "SerilogSyntax.Tests\bin\$Configuration\net472\SerilogSyntax.Tests.dll"

    if (-not (Test-Path $testDll)) {
        Write-Error "Test assembly not found: $testDll"
        exit 1
    }

    $args = @($testDll, "/Logger:console;verbosity=minimal")
    if ($Filter) {
        $args += "/TestCaseFilter:FullyQualifiedName~$Filter"
    }

    $totalPassed = 0
    $totalFailed = 0
    $failedIterations = @()

    for ($i = 1; $i -le $Iterations; $i++) {
        if ($Iterations -gt 1) {
            Write-Host "`nRunning tests (iteration ${i} of ${Iterations})..." -ForegroundColor Cyan
        } else {
            Write-Host "`nRunning tests..." -ForegroundColor Cyan
        }

        & "$vstest" @args

        if ($LASTEXITCODE -eq 0) {
            $totalPassed++
            if ($Iterations -gt 1) {
                Write-Host "Iteration ${i}: PASSED" -ForegroundColor Green
            }
        } else {
            $totalFailed++
            $failedIterations += $i
            if ($Iterations -gt 1) {
                Write-Host "Iteration ${i}: FAILED" -ForegroundColor Red
            }
        }
    }

    # Summary for multiple iterations
    if ($Iterations -gt 1) {
        Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
        Write-Host "Total iterations: ${Iterations}" -ForegroundColor White
        Write-Host "Passed: ${totalPassed}" -ForegroundColor Green
        Write-Host "Failed: ${totalFailed}" -ForegroundColor Red
        
        if ($totalFailed -gt 0) {
            Write-Host "Failed iterations: $($failedIterations -join ', ')" -ForegroundColor Red
            $successRate = [math]::Round(($totalPassed / $Iterations) * 100, 1)
            Write-Host "Success rate: ${successRate}%" -ForegroundColor Yellow
            Write-Host "`nFlaky tests detected!" -ForegroundColor Red
            exit 1
        } else {
            Write-Host "`nAll iterations passed - tests are stable!" -ForegroundColor Green
        }
    } else {
        if ($LASTEXITCODE -eq 0) {
            Write-Host "`nAll tests passed!" -ForegroundColor Green
        } else {
            Write-Host "`nTests failed!" -ForegroundColor Red
            exit $LASTEXITCODE
        }
    }
} finally {
    Pop-Location
}