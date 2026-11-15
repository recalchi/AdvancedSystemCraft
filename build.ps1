$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$out = Join-Path $PSScriptRoot 'build/v0.2.0'
New-Item -ItemType Directory -Force -Path $out | Out-Null
& $compiler /nologo /target:winexe /optimize+ "/out:$out\Advanced System Craft.exe" /reference:System.Management.dll /reference:Microsoft.VisualBasic.dll /reference:Microsoft.CSharp.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\WindowsHealth.cs" "$PSScriptRoot\Engine.cs" "$PSScriptRoot\App.cs"
if ($LASTEXITCODE) { throw 'Falha na compilação' }
