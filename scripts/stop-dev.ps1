$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'local-env.ps1')
$state = Join-Path $MiniErpWork 'processes.json'
$savedIds = if (Test-Path -LiteralPath $state) { (Get-Content -LiteralPath $state -Raw | ConvertFrom-Json).Id }
$listenerIds = (Get-NetTCPConnection -LocalPort 5168,5186 -State Listen -ErrorAction SilentlyContinue).OwningProcess
$entryPoints = $MiniErpApi, (Join-Path $MiniErpWork 'api-run\MiniErp.Api.dll'),
    (Join-Path $MiniErpRoot 'frontend\node_modules\vite\bin\vite.js')
foreach ($processId in @($savedIds) + @($listenerIds) | Where-Object { $_ -gt 0 } | Select-Object -Unique) {
    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $([int]$processId)"
    if ($process -and $process.Name -in 'dotnet.exe','node.exe' -and
        ($entryPoints | Where-Object { $process.CommandLine -like ('*' + $_ + '*') })) {
        $running = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($running) {
            Stop-Process -InputObject $running
            if (!$running.WaitForExit(5000)) { throw "MiniErp process $processId is still running." }
        }
    }
}
if (Test-Path -LiteralPath $state) { Remove-Item -LiteralPath $state }
