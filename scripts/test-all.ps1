param([switch]$Browser)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'local-env.ps1')
Set-MiniErpLocalEnvironment -Test

Push-Location $MiniErpRoot
try {
    dotnet test MiniErp.sln --no-restore --logger 'console;verbosity=normal'
    if ($LASTEXITCODE) { throw 'Backend tests failed.' }
    foreach ($command in 'lint','typecheck','test','build') {
        npm.cmd --prefix frontend run $command
        if ($LASTEXITCODE) { throw "Frontend $command failed." }
    }
    if ($Browser) {
        npm.cmd --prefix frontend run test:e2e
        if ($LASTEXITCODE) { throw 'Playwright tests failed.' }
    }
} finally {
    Pop-Location
}
