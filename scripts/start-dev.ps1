$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'local-env.ps1')
Set-MiniErpLocalEnvironment

$state = Join-Path $MiniErpWork 'processes.json'
if (Test-Path -LiteralPath $state) {
    throw 'Run scripts/stop-dev.ps1 before restarting this project.'
}
if (Get-NetTCPConnection -LocalPort 5168,5186 -State Listen -ErrorAction SilentlyContinue) {
    throw 'A MiniErp development port (5168 or 5186) is in use.'
}
$vite = Join-Path $MiniErpRoot 'frontend\node_modules\vite\bin\vite.js'
if (!(Test-Path -LiteralPath $MiniErpApi) -or !(Test-Path -LiteralPath $vite)) {
    throw 'Run scripts/setup.ps1 first.'
}

$runDirectory = Join-Path $MiniErpWork 'api-run'
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
Copy-Item -Path (Join-Path (Split-Path $MiniErpApi -Parent) '*') -Destination $runDirectory -Recurse -Force
$runApi = Join-Path $runDirectory (Split-Path $MiniErpApi -Leaf)

$started = @()
try {
    $started += Start-Process dotnet -ArgumentList "`"$runApi`" --urls http://127.0.0.1:5168" -WorkingDirectory $runDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $MiniErpWork 'api.log') -RedirectStandardError (Join-Path $MiniErpWork 'api-error.log')
    $started += Start-Process node -ArgumentList "`"$vite`" --host localhost --port 5186 --strictPort" -WorkingDirectory (Join-Path $MiniErpRoot 'frontend') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $MiniErpWork 'frontend.log') -RedirectStandardError (Join-Path $MiniErpWork 'frontend-error.log')
    $entries = @($started | ForEach-Object { @{ Id = $_.Id } })
    ConvertTo-Json -InputObject $entries | Set-Content -LiteralPath $state
} catch {
    $started | Where-Object { !$_.HasExited } | Stop-Process -ErrorAction SilentlyContinue
    throw
}

Write-Output 'Frontend http://localhost:5186'
Write-Output 'API http://127.0.0.1:5168'
