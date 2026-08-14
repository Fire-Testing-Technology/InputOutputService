<#
.SYNOPSIS
  Publishes the API for linux-arm64 and deploys it to the Raspberry Pi for Visual Studio remote debugging.

.DESCRIPTION
  Visual Studio does not F5 to Linux over SSH by default. Use this script to deploy and start the app,
  then in VS: Debug > Attach to Process > Connection type SSH > pi@192.168.1.64 >
  select the process > Managed (.NET Core for Unix).

.PARAMETER Start
  Start the app on the Pi after deploy (background). Required before Attach unless you start it yourself.

.PARAMETER HostName
  SSH host (default 192.168.1.64).

.PARAMETER UserName
  SSH user (default pi).
#>
[CmdletBinding()]
param(
    [switch]$Start,
    [string]$HostName = "192.168.1.64",
    [string]$UserName = "pi",
    [string]$RemoteDir = "/home/pi/modbus-debug"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$project = Join-Path $repoRoot "InputOutput.ModbusMaster.Api\InputOutput.ModbusMaster.Api.csproj"
$publishDir = Join-Path $repoRoot ".debug\pi"
$sshTarget = "${UserName}@${HostName}"

Write-Host "Publishing Debug linux-arm64 to $publishDir"
dotnet publish $project `
    -c Debug `
    -r linux-arm64 `
    --self-contained false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Deploying to ${sshTarget}:${RemoteDir}"
ssh -o BatchMode=yes $sshTarget "pkill -f InputOutput.ModbusMaster.Api || true; mkdir -p '$RemoteDir'"
scp -o BatchMode=yes -r "$publishDir/." "${sshTarget}:${RemoteDir}/"
ssh -o BatchMode=yes $sshTarget "chmod +x '$RemoteDir/InputOutput.ModbusMaster.Api'"

# appsettings.json binds Kestrel to localhost; rewrite so the PC can reach the Pi.
ssh -o BatchMode=yes $sshTarget "sed -i 's#http://localhost:5217#http://0.0.0.0:5217#; s#https://localhost:7015#https://0.0.0.0:7015#' '$RemoteDir/appsettings.json'"

if ($Start) {
    Write-Host "Starting app on Pi (http://0.0.0.0:5217)"
    $bash = @"
set -e
export DOTNET_ROOT=`$HOME/.dotnet
export PATH=`$DOTNET_ROOT:`$PATH
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://0.0.0.0:5217
cd "$RemoteDir"
pkill -f InputOutput.ModbusMaster.Api || true
nohup `$DOTNET_ROOT/dotnet InputOutput.ModbusMaster.Api.dll > /tmp/modbus-api.log 2>&1 &
echo `$!
"@
    $bash = ($bash -replace "`r`n", "`n" -replace "`r", "`n")
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($bash))
    $pidRemote = ssh -o BatchMode=yes $sshTarget "echo $b64 | base64 -d | bash"
    Write-Host "Started PID $pidRemote  log: /tmp/modbus-api.log"
    Write-Host "In Visual Studio: Debug > Attach to Process > SSH > $sshTarget > Managed (.NET Core for Unix)"
    Write-Host "UI: http://${HostName}:5217"
}
else {
    Write-Host 'Deployed. Run again with -Start, or start manually on the Pi, then Attach in Visual Studio.'
}
