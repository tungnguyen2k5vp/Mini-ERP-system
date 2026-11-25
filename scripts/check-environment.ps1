$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'local-env.ps1')
[Environment]::OSVersion.VersionString
dotnet --list-sdks
git --version
node --version
npm.cmd --version
if (Get-Command docker -ErrorAction SilentlyContinue) { docker compose version }
if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) { sqllocaldb versions }
if (Get-Command sqlcmd -ErrorAction SilentlyContinue) {
    sqlcmd -S lpc:localhost -E -C -l 5 -b -Q "SET NOCOUNT ON; SELECT SERVERPROPERTY('ProductVersion') AS SqlVersion, SERVERPROPERTY('Edition') AS Edition;"
}
