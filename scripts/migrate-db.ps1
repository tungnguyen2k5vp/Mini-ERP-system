param([switch]$Test)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'local-env.ps1')
Set-MiniErpLocalEnvironment -Test:$Test

Push-Location (Split-Path $MiniErpApi -Parent)
try {
    dotnet $MiniErpApi --migrate
    if ($LASTEXITCODE) { throw 'EF Core migration failed.' }
} finally {
    Pop-Location
}
