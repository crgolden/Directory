#Requires -Version 7
# Runs the smoke test suite.
# Credentials are read from User Secrets (ID 5480cab8-b41b-4dae-8c41-dbc2c01a15e0)
# so they never need to be set as OS environment variables.
#
# Local (default): targets https://localhost:7150 — requires Identity, Experience, and Products
# running locally. Identity must use reCAPTCHA test keys (set via its User Secrets).
#
# Deployed: pass -BaseUrl https://crgolden-experience.azurewebsites.net
param(
    [string]$BaseUrl = "https://crgolden-experience.azurewebsites.net"
)

$secretsPath = Join-Path $env:APPDATA "Microsoft\UserSecrets\5480cab8-b41b-4dae-8c41-dbc2c01a15e0\secrets.json"
$secrets     = Get-Content $secretsPath -Raw | ConvertFrom-Json

$env:SmokeBaseUrl = $BaseUrl
$env:AdminEmail  = $secrets.AdminEmail
$env:AdminPassword  = $secrets.AdminPassword

try
{
    & ".\Experience.Tests\bin\Debug\net10.0\Experience.Tests.exe" -trait "Category=Smoke" -showLiveOutput
}
finally
{
    Remove-Item Env:SmokeBaseUrl, Env:AdminEmail, Env:AdminPassword -ErrorAction SilentlyContinue
}
