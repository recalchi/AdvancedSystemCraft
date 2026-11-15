param([switch]$Verification)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$out = Join-Path $PSScriptRoot 'build/v0.6.2'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File |
    Where-Object { $Verification -or $_.Name -ne 'Verification.cs' } |
    Sort-Object Name | ForEach-Object FullName)
$flags = @('/nologo','/optimize+','/win32manifest:app.manifest','/reference:System.Management.dll','/reference:Microsoft.VisualBasic.dll',
    '/reference:Microsoft.CSharp.dll','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll')
if ($Verification) {
    $flags += '/define:CRAFT_REGISTRY_TESTS','/target:exe','/main:Verification',"/out:$PSScriptRoot\build\Verification.exe"
} else {
    $flags += '/target:winexe',"/out:$out\Advanced System Craft.exe"
}
& $compiler @flags @sources
if ($LASTEXITCODE) { throw 'Falha na compilação do Advanced System Craft' }
