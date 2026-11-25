$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'local-env.ps1')

Push-Location $MiniErpRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE) { throw 'dotnet tool restore failed.' }
    dotnet restore MiniErp.sln --locked-mode
    if ($LASTEXITCODE) { throw 'NuGet restore failed.' }
    dotnet build MiniErp.sln --no-restore
    if ($LASTEXITCODE) { throw 'Backend build failed.' }
    npm.cmd --prefix frontend ci --ignore-scripts
    if ($LASTEXITCODE) { throw 'npm ci failed.' }
} finally {
    Pop-Location
}
