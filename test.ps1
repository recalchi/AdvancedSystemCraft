param([string]$Preview,[switch]$Catalog)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$runner = Join-Path $PSScriptRoot 'build/Verification.exe'
& $compiler /nologo /target:exe /main:Verification "/out:$runner" /reference:System.Management.dll /reference:Microsoft.VisualBasic.dll /reference:Microsoft.CSharp.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\Performance.cs" "$PSScriptRoot\WindowsHealth.cs" "$PSScriptRoot\Engine.cs" "$PSScriptRoot\App.cs" "$PSScriptRoot\Verification.cs"
if ($LASTEXITCODE) { throw 'Falha na compilação dos testes' }
$arguments = @()
if ($Preview) { $arguments += $Preview }
if ($Catalog) { $arguments += "--catalog" }
& $runner @arguments
if ($LASTEXITCODE) { throw 'Falha nos testes' }
