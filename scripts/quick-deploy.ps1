# quick-deploy.ps1 - Build and deploy in one step
#
# PURPOSE:
#   Convenience script that combines build.ps1 and deploy.ps1 into a single command.
#   Perfect for rapid development iterations where you want to test changes quickly.
#
# EXAMPLES:
#   .\quick-deploy.ps1                        # Build Debug and deploy to primary instance
#   .\quick-deploy.ps1 -RestartVS            # Build, deploy to primary, and restart VS
#   .\quick-deploy.ps1 -NoBuild              # Skip build, just deploy existing VSIX
#   .\quick-deploy.ps1 -Configuration Release # Build Release and deploy
#   .\quick-deploy.ps1 -NoBuild -RestartVS   # Deploy existing build and restart
#   .\quick-deploy.ps1 -Experimental         # Deploy to Experimental instance only
#   .\quick-deploy.ps1 -All                  # Deploy to ALL instances
#
# TYPICAL WORKFLOW:
#   1. Make code changes in your editor
#   2. Run: .\quick-deploy.ps1 -RestartVS
#   3. VS restarts with your changes loaded
#   4. Test your changes
#   5. Repeat
#
# PARAMETERS:
#   -Configuration: Debug or Release (default: Debug)
#   -NoBuild: Skip the build step if you've already built
#   -RestartVS: Automatically restart VS after deployment
#
# NOTES:
#   - This is faster than using the VS debugger (F5) for testing small changes
#   - Changes go to your regular VS instance, not the Experimental instance
#   - If something goes wrong, use the VS Extension Manager to uninstall/reinstall
#
param(
    [string]$Configuration = "Debug",  # Build configuration (Debug/Release)
    [switch]$NoBuild,                  # Skip build step if VSIX already exists
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

# Build first unless skipped
if (-not $NoBuild) {
    Write-Host "Building solution..." -ForegroundColor Cyan
    $buildScript = Join-Path $scriptDir "build.ps1"
    & $buildScript -Configuration $Configuration
    
    # Check if build succeeded
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}

# Deploy the extension
# This will update the DLLs in-place without reinstalling the VSIX
Write-Host "`nDeploying extension..." -ForegroundColor Cyan
$deployScript = Join-Path $scriptDir "deploy.ps1"
& $deployScript -Configuration $Configuration -RestartVS:$RestartVS -Experimental:$Experimental -All:$All

# Success message
Write-Host "`nDeployment complete!" -ForegroundColor Green

# Reminder if VS wasn't auto-restarted
if (-not $RestartVS) {
    Write-Host "Remember to restart Visual Studio to see your changes." -ForegroundColor Yellow
}