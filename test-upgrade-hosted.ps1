param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Este ensaio de instalação só pode rodar em uma VM Windows hospedada pelo GitHub Actions.'
}

$winget = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\winget.exe'
function Test-WinGet {
    if (!(Test-Path -LiteralPath $winget)) { return $false }
    try { & $winget --version | Out-Host; return $LASTEXITCODE -eq 0 } catch { return $false }
}
if (!(Test-WinGet)) {
    # Registration and stable bootstrap documented at learn.microsoft.com/windows/package-manager/winget/
    try { Add-AppxPackage -RegisterByFamilyName -MainPackage Microsoft.DesktopAppInstaller_8wekyb3d8bbwe } catch { Write-Host $_ }
    if (!(Test-WinGet)) {
        Install-PackageProvider -Name NuGet -Force | Out-Null
        Install-Module -Name Microsoft.WinGet.Client -Scope CurrentUser -Force -Repository PSGallery
        Repair-WinGetPackageManager -AllUsers
    }
    if (!(Test-WinGet)) { throw 'O alias WinGet usado pelo Craft está indisponível.' }
}

$packageId = 'sharkdp.hyperfine'
$baseline = '1.18.0'
# This portable executable supplies its own independent --version observation.
# Use only the per-user package we install, never a different copy from PATH.
function Get-InstalledBinaryVersion {
    $alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\hyperfine.exe'
    if (!(Test-Path -LiteralPath $alias)) {
        $root = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages'
        $dirs = @(Get-ChildItem -LiteralPath $root -Directory -Filter 'sharkdp.hyperfine_*' -ErrorAction SilentlyContinue)
        $matches = @($dirs | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -File -Recurse -Filter hyperfine.exe })
        if ($matches.Count -ne 1) { return $null }
        $alias = $matches[0].FullName
    }
    $reported = & $alias --version 2>$null
    if ($LASTEXITCODE -eq 0 -and $reported -match '^hyperfine\s+(\S+)\s*$') { return $Matches[1] }
    return $null
}

& $winget install --id $packageId --exact --version $baseline --source winget --scope user --architecture x64 --silent --disable-interactivity --accept-source-agreements --accept-package-agreements
if ($LASTEXITCODE -ne 0) { throw "Falha ao preparar a versão $baseline (código $LASTEXITCODE)." }
$before = Get-InstalledBinaryVersion
if ($before -ne $baseline) { throw "Versão do binário após a instalação inicial: '$before'; esperada: '$baseline'." }
Write-Host "Antes: $packageId $before"

Add-Type -AssemblyName System.Windows.Forms
[Windows.Forms.Application]::EnableVisualStyles()
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $Executable).Path)
$window = [Activator]::CreateInstance($assembly.GetType('CraftWindow',$true))
function Get-Button($parent,[string]$name) {
    foreach ($control in $parent.Controls) {
        if ($control -is [Windows.Forms.Button] -and $control.Text -eq $name) { return $control }
        $found = Get-Button $control $name
        if ($null -ne $found) { return $found }
    }
}
function Get-PackageRow([string]$query) {
    $button = Get-Button $window $query
    $button.PerformClick()
    $end = [DateTime]::UtcNow.AddMinutes(3)
    while (!$button.Enabled -and [DateTime]::UtcNow -lt $end) {
        [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 75
    }
    if (!$button.Enabled) { throw "Tempo esgotado ao executar $query." }
    $list = $window.Controls.Find('catalog-packages',$true)[0]
    $rows = @($list.Items | Where-Object { $_.SubItems[3].Text -ceq $packageId })
    if ($rows.Count -ne 1) { throw "A consulta '$query' retornou $($rows.Count) linhas para $packageId." }
    return $rows[0]
}

try {
    $window.Show()
    (Get-Button $window 'Software').PerformClick()
    $row = Get-PackageRow 'Buscar atualizações'
    $offered = $row.SubItems[2].Text
    if ($row.SubItems[1].Text -ne $before -or $offered -notmatch '^\d+\.\d+\.\d+$' -or [version]$offered -le [version]$before) {
        throw "Prévia inesperada: versão instalada '$($row.SubItems[1].Text)', disponível '$offered'."
    }
    Write-Host "Prévia do Craft: $packageId $before -> $offered"
    $row.Selected = $true
    [Windows.Forms.Application]::DoEvents()
    $update = Get-Button $window 'Atualizar selecionado'
    if (!$update.Enabled) { throw 'O botão de atualização individual está desabilitado.' }
    $update.PerformClick()
    $end = [DateTime]::UtcNow.AddMinutes(4)
    do {
        [Windows.Forms.Application]::DoEvents()
        Start-Sleep -Seconds 2
        try { $after = Get-InstalledBinaryVersion } catch { $after = $null }
    } while ($after -ne $offered -and [DateTime]::UtcNow -lt $end)
    if ($after -ne $offered) { throw "Versão esperada '$offered'; última leitura do executável: '$after'." }
    $row = Get-PackageRow 'Listar instalados'
    if ($row.SubItems[1].Text -ne $after) { throw "Catálogo e executável discordam após a instalação: '$($row.SubItems[1].Text)' e '$after'." }
    $proof = [ordered]@{package=$packageId;installed_before=$before;offered_by_craft=$offered;installed_after=$after;catalog_after=$row.SubItems[1].Text;action='Atualizar selecionado';commit=$env:GITHUB_SHA}
    $proof | ConvertTo-Json | Set-Content -LiteralPath "$env:RUNNER_TEMP/software-upgrade-witness.json" -Encoding UTF8
    Write-Host "PASS atualização individual: $packageId $before -> $after, confirmada pelo executável e pela interface."
} finally {
    $window.Dispose()
}
