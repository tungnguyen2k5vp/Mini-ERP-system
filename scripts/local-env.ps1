$MiniErpRoot = Split-Path $PSScriptRoot -Parent
$MiniErpWork = Join-Path $MiniErpRoot '.work'
$MiniErpApi = Join-Path $MiniErpRoot 'backend\src\MiniErp.Api\bin\Debug\net10.0\MiniErp.Api.dll'
$env:DOTNET_CLI_HOME = Join-Path $MiniErpWork 'dotnet'
$env:NUGET_PACKAGES = Join-Path $MiniErpWork 'nuget'
$env:npm_config_cache = Join-Path $MiniErpWork 'npm'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

function Set-MiniErpLocalEnvironment([switch]$Test) {
    $settingsPath = Join-Path $MiniErpWork 'dev.env.json'
    if (!(Test-Path -LiteralPath $settingsPath)) {
        New-Item -ItemType Directory -Path $MiniErpWork -Force | Out-Null
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $key = New-Object byte[] 48
            $password = New-Object byte[] 24
            $rng.GetBytes($key)
            $rng.GetBytes($password)
            $newSettings = @{
                SigningKey = [Convert]::ToBase64String($key)
                AdminEmail = 'admin@minierp.example'
                AdminPassword = [Convert]::ToBase64String($password) + 'Aa9!'
            }
            New-Item -ItemType File -Path $settingsPath -Value ($newSettings | ConvertTo-Json) -ErrorAction Stop | Out-Null
        } finally {
            $rng.Dispose()
        }
    }

    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    $database = if ($Test) { 'MiniErp_Test' } else { 'MiniErp' }
    $env:ConnectionStrings__Erp = "Server=lpc:localhost;Database=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
    if ($Test -and $env:MINIERP_TEST_CONNECTION) {
        $testConnection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($env:MINIERP_TEST_CONNECTION)
        if ($testConnection.InitialCatalog -notmatch '^MiniErp_Test(?:_[A-Za-z0-9_]+)?$') {
            throw 'MINIERP_TEST_CONNECTION must point to an isolated MiniErp_Test database.'
        }
        $env:ConnectionStrings__Erp = $env:MINIERP_TEST_CONNECTION
    }

    $env:ASPNETCORE_ENVIRONMENT = if ($Test) { 'Testing' } else { 'Development' }
    $env:Auth__SigningKey = $settings.SigningKey
    $env:Auth__PublicOrigin = 'http://localhost:5186'
    $env:Seed__AdminEmail = $settings.AdminEmail
    $env:Seed__AdminPassword = $settings.AdminPassword
    $env:MINIERP_E2E_EMAIL = $settings.AdminEmail
    $env:MINIERP_E2E_PASSWORD = $settings.AdminPassword
}
