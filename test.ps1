param([string]$Preview,[switch]$Catalog)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
& (Join-Path $PSScriptRoot 'build.ps1') -Verification
$runner = Join-Path $PSScriptRoot 'build/Verification.exe'
$arguments = @()
if ($Preview) { $arguments += $Preview }
if ($Catalog) { $arguments += "--catalog" }
& $runner @arguments
if ($LASTEXITCODE) { throw 'Falha nos testes' }
