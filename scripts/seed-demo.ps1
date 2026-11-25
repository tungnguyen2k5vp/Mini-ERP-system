param([switch]$Test)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'local-env.ps1')
Set-MiniErpLocalEnvironment -Test:$Test

Push-Location (Split-Path $MiniErpApi -Parent)
try {
    dotnet $MiniErpApi --seed
    if ($LASTEXITCODE) { throw 'Demo seed failed.' }
} finally {
    Pop-Location
}
