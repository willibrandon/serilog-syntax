# deploy.ps1 - Fast VSIX deployment without reinstall
#
# PURPOSE:
#   Updates an already-installed VSIX extension in-place by copying new DLLs directly
#   to the VS extension folder. This is MUCH faster than uninstall/reinstall cycle.
#
# EXAMPLES:
#   .\deploy.ps1                        # Deploy to primary VS instance only
#   .\deploy.ps1 -Configuration Release # Deploy Release build to primary instance
#   .\deploy.ps1 -RestartVS            # Deploy and auto-restart Visual Studio
#   .\deploy.ps1 -Experimental          # Deploy to Experimental instance only
#   .\deploy.ps1 -All                   # Deploy to ALL instances (regular + Experimental)
#   .\deploy.ps1 -All -RestartVS        # Update everything and restart VS
#
# HOW IT WORKS:
#   1. Finds all VS instances with this extension installed
#   2. Extracts the VSIX (it's just a ZIP file) to temp folder
#   3. Copies new DLLs/files directly to installation folders
#   4. Clears MEF cache so VS picks up the changes
#   5. Optionally restarts VS if -RestartVS is specified
#
# FIRST-TIME INSTALL:
#   If extension is not installed, falls back to VSIXInstaller for initial setup
#
# TROUBLESHOOTING:
#   - If changes don't appear, manually restart VS
#   - If VS acts weird, clear cache: delete %LOCALAPPDATA%\Microsoft\VisualStudio\17.0*\ComponentModelCache
#   - For clean slate: uninstall extension, delete cache, reinstall
#
param(
    [string]$Configuration = "Debug",  # Build configuration to deploy (Debug/Release)
    [switch]$RestartVS,                # Auto-restart VS after deployment
    [switch]$Experimental,              # Target Experimental instance instead of regular
    [switch]$All                       # Update ALL instances (both regular and Experimental)
)

# Determine the root directory (parent of scripts folder or current directory)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($scriptDir -match "scripts$") {
    $rootDir = Split-Path -Parent $scriptDir
} else {
    $rootDir = $scriptDir
}

# Extension ID from source.extension.vsixmanifest
$extensionId = "SerilogSyntax.33851f71-44fa-46e0-9f66-e0f039ca2681"

# Path to the built VSIX file - resolve to absolute path
$vsixPath = Join-Path $rootDir "SerilogSyntax\bin\$Configuration\SerilogSyntax.vsix"

# Check if VSIX exists
if (-not (Test-Path $vsixPath)) {
    Write-Host "VSIX not found at $vsixPath. Run build.ps1 first." -ForegroundColor Red
    exit 1
}

# Find the currently running VS instance's extension installation
# First, try to detect which VS instance is running
$runningVsProcess = Get-Process devenv -ErrorAction SilentlyContinue | Select-Object -First 1
$installedPaths = @()

if ($runningVsProcess) {
    # Get the VS installation path from the running process
    $vsExePath = $runningVsProcess.Path
    Write-Host "Found running VS at: $vsExePath" -ForegroundColor Cyan
    
    # Extract the instance ID from the process command line or use default search
    # VS stores extensions in %LOCALAPPDATA%\Microsoft\VisualStudio\{instance}\Extensions
}

# Determine which instances to target
if ($All) {
    # Target all instances (both regular and Experimental)
    $vsVersions = @("17.0_*", "18.0_*")
    Write-Host "Targeting ALL VS instances (regular and Experimental)" -ForegroundColor Cyan
} elseif ($Experimental) {
    # Target only Experimental instances
    $vsVersions = @("17.0_*Exp", "18.0_*Exp")
    Write-Host "Targeting Experimental VS instances only" -ForegroundColor Cyan
} else {
    # Target only regular (non-Experimental) instances
    $vsVersions = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\VisualStudio" -Directory -ErrorAction SilentlyContinue | 
        Where-Object { $_.Name -match "^1[78]\.0_[a-f0-9]+$" } | 
        Select-Object -ExpandProperty Name
    
    if ($vsVersions) {
        Write-Host "Targeting regular VS instance(s)" -ForegroundColor Cyan
    }
}

# Search for the extension in selected VS instances
foreach ($vsVersion in $vsVersions) {
    $extensionPath = "$env:LOCALAPPDATA\Microsoft\VisualStudio\$vsVersion\Extensions"
    if (Test-Path $extensionPath) {
        $found = Get-ChildItem $extensionPath -Filter "extension.vsixmanifest" -Recurse -ErrorAction SilentlyContinue | 
            Where-Object { 
                $content = Get-Content $_.FullName -Raw
                $content -match [regex]::Escape($extensionId)
            } |
            ForEach-Object { Split-Path $_.FullName -Parent }
        
        if ($found) {
            $installedPaths += $found
        }
    }
}

# If not using -All flag and multiple installations found, use only the first one
if (-not $All -and $installedPaths.Count -gt 1) {
    Write-Host "Found $($installedPaths.Count) installations. Updating first instance only." -ForegroundColor Yellow
    Write-Host "Use -All flag to update all instances." -ForegroundColor Yellow
    $installedPaths = @($installedPaths[0])
}

if ($installedPaths.Count -eq 0) {
    Write-Host "Extension not installed. Installing for the first time..." -ForegroundColor Yellow
    
    # Use VSIXInstaller for first-time installation
    # Try to find VSIXInstaller.exe in common locations
    $vsixInstaller = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\resources\app\ServiceHub\Services\Microsoft.VisualStudio.Setup.Service\VSIXInstaller.exe"
    if (-not (Test-Path $vsixInstaller)) {
        # Try alternate path
        $vsixInstaller = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe"
    }
    
    if (Test-Path $vsixInstaller) {
        Write-Host "Installing VSIX..." -ForegroundColor Cyan
        & $vsixInstaller /quiet $vsixPath
        Write-Host "Installation complete. Restart Visual Studio to use the extension." -ForegroundColor Green
    } else {
        Write-Host "VSIXInstaller not found. Please install manually by double-clicking: $vsixPath" -ForegroundColor Yellow
    }
    exit 0
}

# Close VS if requested
if ($RestartVS) {
    Write-Host "Closing Visual Studio instances..." -ForegroundColor Yellow
    Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}

# Extract VSIX contents to temp directory
# VSIX files are just ZIP files with a different extension
$tempDir = Join-Path $env:TEMP "SerilogSyntax_Deploy_$(Get-Date -Format 'yyyyMMddHHmmss')"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

Write-Host "Extracting VSIX contents from: $vsixPath" -ForegroundColor Cyan
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($vsixPath, $tempDir)
} catch {
    Write-Host "Failed to extract VSIX: $_" -ForegroundColor Red
    Write-Host "VSIX path: $vsixPath" -ForegroundColor Red
    Write-Host "Temp directory: $tempDir" -ForegroundColor Red
    
    # Clean up on failure
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force
    }
    exit 1
}

# Update each installed instance
foreach ($installedPath in $installedPaths) {
    Write-Host "Updating extension at: $installedPath" -ForegroundColor Cyan
    
    # Copy all DLLs and PDB files (the actual code of the extension)
    Copy-Item "$tempDir\*.dll" $installedPath -Force
    Copy-Item "$tempDir\*.pdb" $installedPath -Force -ErrorAction SilentlyContinue
    
    # Copy pkgdef file if it exists (package definitions)
    if (Test-Path "$tempDir\*.pkgdef") {
        Copy-Item "$tempDir\*.pkgdef" $installedPath -Force
    }
    
    # Update the manifest to trigger VS reload
    $manifestPath = Join-Path $installedPath "extension.vsixmanifest"
    if (Test-Path "$tempDir\extension.vsixmanifest") {
        Copy-Item "$tempDir\extension.vsixmanifest" $manifestPath -Force
    }
    
    # Touch the manifest to ensure VS notices the change
    # This forces VS to recognize that the extension has been updated
    (Get-Item $manifestPath).LastWriteTime = Get-Date
    
    Write-Host "Updated: $installedPath" -ForegroundColor Green
}

# Clean up temp directory
Remove-Item $tempDir -Recurse -Force

# Clear MEF cache to ensure changes are picked up
# MEF (Managed Extensibility Framework) caches component metadata
# If we don't clear this, VS might continue using old versions of our code
$mefCachePaths = @(
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\17.0*\ComponentModelCache",
    "$env:LOCALAPPDATA\Microsoft\VisualStudio\18.0*\ComponentModelCache"
)

foreach ($cachePath in $mefCachePaths) {
    $caches = Get-Item $cachePath -ErrorAction SilentlyContinue
    foreach ($cache in $caches) {
        if (Test-Path $cache) {
            Write-Host "Clearing MEF cache: $cache" -ForegroundColor Cyan
            Remove-Item "$cache\*" -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Write-Host "`nExtension updated successfully!" -ForegroundColor Green
Write-Host "Restart Visual Studio to see changes." -ForegroundColor Yellow

if ($RestartVS) {
    Write-Host "`nRestarting Visual Studio..." -ForegroundColor Cyan
    Start-Sleep -Seconds 1
    Start-Process devenv.exe
}