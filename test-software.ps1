param([Parameter(Mandatory=$true)][string]$Executable,[switch]$Live)
$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path $Executable).Path)
$catalog=$assembly.GetType('CatalogResult')
$health=$assembly.GetType('WindowsHealth')
$static=[Reflection.BindingFlags]'Static,NonPublic,Public'
$fields=[Reflection.BindingFlags]'Instance,NonPublic,Public'
function Read-Field($instance,$name) { $instance.GetType().GetField($name,$fields).GetValue($instance) }
function Check($condition,$name) { if(!$condition){throw $name}; "PASS $name" }
function Parse($text,$code=0) { $catalog.GetMethod('FromOutput',$static).Invoke($null,@($text,'',[int]$code)) }
$table=("{0,-22}{1,-16}{2,-14}{3}" -f 'Nome','Id','Versão','Disponível')+"`n"+('-'*66)+"`n"
$table+=("{0,-22}{1,-16}{2,-14}{3}" -f 'Piloto A','Fixture.Package','< 20','20.1')+"`n"
$table+=("{0,-22}{1,-16}{2,-14}{3}" -f 'Aplicativo B','App.B','37.0.6','')+"`n"
$table+=("{0,-22}{1,-16}{2,-14}{3}" -f 'ID truncado','App.Trunc...','1','2')
$result=Parse $table
$rows=$result.GetType().GetField('Packages',$fields).GetValue($result)
Check ($rows.Count -eq 2) 'somente identidades completas ficam selecionáveis'
Check ((Read-Field $rows[0] 'Installed') -eq '< 20' -and (Read-Field $rows[0] 'Available') -eq '20.1') 'versões preservadas'
Check ((Read-Field $rows[1] 'Available') -eq '') 'versão disponível ausente continua desconhecida'
Check ((Read-Field $result 'Text').Contains('App.Trunc...')) 'linha sem ID completo continua disponível no original'
$command=$health.GetMethod('UpdateCommand',$static).Invoke($null,@((Read-Field $rows[0] 'Id')))
Check ($command.Arguments.Contains('--id Fixture.Package --exact')) 'ação do pacote usa ID exato da linha'
$unknown=Parse 'Nova saída do catálogo'
Check (!(Read-Field $unknown 'Recognized') -and (Read-Field $unknown 'Text') -eq 'Nova saída do catálogo') 'formato futuro mantém saída original'
if($Live){
    $task=$health.GetMethod('ListSoftware',$static).Invoke($null,@($true))
    $actual=$task.GetAwaiter().GetResult()
    $detected=$actual.GetType().GetField('Packages',$fields).GetValue($actual)
    Check ((Read-Field $actual 'Success') -and $detected.Count -gt 0) 'consulta local retorna linhas selecionáveis'
    "WinGet: $($detected.Count) linhas com ID."
}
if($Live){
    Add-Type -AssemblyName System.Windows.Forms
    [Windows.Forms.Application]::EnableVisualStyles()
    $window=[Activator]::CreateInstance($assembly.GetType('CraftWindow'))
    function Find-Button($parent,$title){
        foreach($control in $parent.Controls){
            if($control -is [Windows.Forms.Button] -and $control.Text -eq $title){return $control}
            $match=Find-Button $control $title
            if($null -ne $match){return $match}
        }
    }
    try {
        $window.Show()
        (Find-Button $window 'Software').PerformClick()
        $fetch=Find-Button $window 'Buscar atualizações'
        $fetch.PerformClick()
        $deadline=(Get-Date).AddSeconds(90)
        while(!$fetch.Enabled -and (Get-Date) -lt $deadline){
            [Windows.Forms.Application]::DoEvents()
            Start-Sleep -Milliseconds 40
        }
        $list=$window.Controls.Find('catalog-packages',$true)[0]
        Check ($list.Items.Count -gt 0) 'janela exibe catálogo consultado'
        $button=Find-Button $window 'Atualizar selecionado'
        Check (!$button.Enabled) 'atualização individual aguarda seleção'
        $list.Items[0].Selected=$true
        [Windows.Forms.Application]::DoEvents()
        Check ($button.Enabled -and (Find-Button $window 'Atualizar todos').Enabled) 'seleção habilita ação individual após prévia'
    }finally{$window.Dispose()}
}
