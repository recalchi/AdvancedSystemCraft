param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient,UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class TestWindow {
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h,int m,IntPtr w,IntPtr l);
}
"@
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('craft-ui-' + [guid]::NewGuid().ToString('N'))
$child = Join-Path $fixture 'subpasta'
$old = Join-Path $child 'antigo.tmp'
$recent = Join-Path $child 'recente.tmp'
$app = $null
function Find-ByName($root, $name) {
    $condition = New-Object Windows.Automation.PropertyCondition([Windows.Automation.AutomationElement]::NameProperty,$name)
    $root.FindFirst([Windows.Automation.TreeScope]::Descendants,$condition)
}
function Invoke-Button($element){
    $invoke=$null
    if($element.TryGetCurrentPattern([Windows.Automation.InvokePattern]::Pattern,[ref]$invoke)){$invoke.Invoke();return}
    if(!$element.Current.NativeWindowHandle -or $element.Current.ClassName -notmatch "BUTTON"){throw ("Controle inesperado: "+$element.Current.ClassName)}
    $null=[TestWindow]::PostMessage([IntPtr]$element.Current.NativeWindowHandle,245,[IntPtr]::Zero,[IntPtr]::Zero)
}
try {
    [IO.Directory]::CreateDirectory($child) | Out-Null
    [IO.File]::WriteAllText($old,'amostra')
    [IO.File]::SetLastWriteTimeUtc($old,[datetime]::UtcNow.AddDays(-10))
    [IO.File]::WriteAllText($recent,'preservar')
    $info=New-Object Diagnostics.ProcessStartInfo
    $info.FileName=(Resolve-Path -LiteralPath $Executable).Path
    $info.UseShellExecute=$false
    $info.EnvironmentVariables['TEMP']=$fixture
    $info.EnvironmentVariables['TMP']=$fixture
    $app=[Diagnostics.Process]::Start($info)
    $null=$app.WaitForInputIdle(10000)
    $deadline=(Get-Date).AddSeconds(12)
    do { $app.Refresh(); if($app.MainWindowHandle -ne 0){break}; Start-Sleep -Milliseconds 100 } while((Get-Date) -lt $deadline)
    $window=[Windows.Automation.AutomationElement]::FromHandle($app.MainWindowHandle)
    foreach($name in @('Atalhos inválidos da Área de Trabalho','Proteção antivírus e firewall','Saúde dos discos físicos','Verificação pendente nos volumes')){
        $option=Find-ByName $window $name
        Invoke-Button $option
    }
    Invoke-Button (Find-ByName $window 'Verificar')
    $deadline=(Get-Date).AddSeconds(20)
    $summary='1 arquivo(s) para revisar · 7 bytes. Marque os itens que deseja enviar à Lixeira.'
    do {
        if(Find-ByName $window $summary){break}
        Start-Sleep -Milliseconds 100
    } while((Get-Date) -lt $deadline)
    if(!(Find-ByName $window $summary)){throw 'O resumo da verificação não corresponde ao único arquivo elegível da amostra.'}
    Invoke-Button (Find-ByName $window 'Marcar todos')
    Start-Sleep -Milliseconds 250
    $button=Find-ByName $window 'Corrigir seleção'
    if(!$button.Current.IsEnabled){throw 'Selecionar o arquivo não habilitou a correção.'}
    Invoke-Button $button
    $deadline=(Get-Date).AddSeconds(10)
    while([IO.File]::Exists($old) -and (Get-Date) -lt $deadline){Start-Sleep -Milliseconds 100}
    if([IO.File]::Exists($old) -or ![IO.File]::Exists($recent)){throw 'Correção inesperada no disco.'}
    Write-Output 'PASS executável: verificar, selecionar e corrigir; o arquivo recente permaneceu.'
} finally {
    if($app -and !$app.HasExited){$null=$app.CloseMainWindow();if(!$app.WaitForExit(3000)){$app.Kill();$app.WaitForExit()}}
    foreach($file in @($recent,$old)){if([IO.File]::Exists($file)){[IO.File]::Delete($file)}}
    foreach($dir in @($child,$fixture)){if([IO.Directory]::Exists($dir) -and [IO.Directory]::GetFileSystemEntries($dir).Length -eq 0){[IO.Directory]::Delete($dir)}}
}
