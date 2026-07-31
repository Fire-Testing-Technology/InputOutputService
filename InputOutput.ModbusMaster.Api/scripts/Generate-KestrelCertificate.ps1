# Regenerates the Kestrel HTTPS certificate used by InputOutput.ModbusMaster.Api.
# Password must match appsettings.json -> Kestrel:Endpoints:Https:Certificate:Password

param(
    [string]$Password = "localhost",
    [string]$DnsName = "localhost",
    [int]$YearsValid = 5
)

$ErrorActionPreference = "Stop"

$apiRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path (Join-Path $apiRoot "InputOutput.ModbusMaster.Api.csproj"))) {
    $apiRoot = Join-Path (Split-Path -Parent $PSScriptRoot) "InputOutput.ModbusMaster.Api"
}

$certsDir = Join-Path $apiRoot "certs"
New-Item -ItemType Directory -Force -Path $certsDir | Out-Null

$pfxPath = Join-Path $certsDir "localhost.pfx"
$cerPath = Join-Path $certsDir "localhost.cer"
$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText

Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.FriendlyName -eq "InputOutputService Kestrel" } |
    Remove-Item -Force -ErrorAction SilentlyContinue

$cert = New-SelfSignedCertificate `
    -DnsName $DnsName `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears($YearsValid) `
    -FriendlyName "InputOutputService Kestrel" `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -HashAlgorithm SHA256 `
    -KeyLength 2048

Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null

Write-Host "Created $pfxPath"
Write-Host "Created $cerPath"
Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host "Password:   $Password"
Write-Host ""
Write-Host "Optional: trust for browsers (Current User Root):"
Write-Host "  Import-Certificate -FilePath `"$cerPath`" -CertStoreLocation Cert:\CurrentUser\Root"
