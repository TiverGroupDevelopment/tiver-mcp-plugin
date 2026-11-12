<#
.SYNOPSIS
    Сборка plugin + commandset для всех версий Revit и раскладка commandset
    в папку команд плагина.

.DESCRIPTION
    Для каждой версии:
      1. dotnet build plugin\RevitMCPPlugin.csproj        -c "<Config> R<yy>"
      2. dotnet build commandset\RevitMCPCommandSet.csproj -c "<Config> R<yy>"
      3. Содержимое %APPDATA%\Autodesk\Revit\Addins\<ver>\RevitMCPCommandSet\
         перемещается в %APPDATA%\Autodesk\Revit\Addins\<ver>\revit_mcp_plugin\Commands\RevitMCPCommandSet\<ver>\
         (с перезаписью), исходная папка удаляется.

    Revit соответствующей версии должен быть закрыт, иначе DLL заблокированы.

.EXAMPLE
    .\scripts\build-all.ps1
.EXAMPLE
    .\scripts\build-all.ps1 -Versions 2019,2020,2024
.EXAMPLE
    .\scripts\build-all.ps1 -Configuration Release
#>
param(
    [ValidateSet(2019, 2020, 2021, 2022, 2023, 2024, 2025, 2026)]
    [int[]]$Versions = @(2019, 2020, 2021, 2022, 2023, 2024, 2025, 2026),

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    # Не собирать, только переместить RevitMCPCommandSet
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root       = Split-Path -Parent $PSScriptRoot
$pluginProj = Join-Path $root 'plugin\RevitMCPPlugin.csproj'
$cmdProj    = Join-Path $root 'commandset\RevitMCPCommandSet.csproj'
$addinsRoot = Join-Path $env:APPDATA 'Autodesk\Revit\Addins'

function Invoke-Build([string]$proj, [string]$config) {
    Write-Host "  dotnet build $(Split-Path -Leaf $proj) -c `"$config`"" -ForegroundColor DarkGray
    & dotnet build $proj -c $config -nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Сборка $(Split-Path -Leaf $proj) ($config) завершилась с кодом $LASTEXITCODE" }
}

function Move-CommandSet([int]$ver) {
    $src = Join-Path $addinsRoot "$ver\RevitMCPCommandSet"
    $dst = Join-Path $addinsRoot "$ver\revit_mcp_plugin\Commands\RevitMCPCommandSet\$ver"

    if (-not (Test-Path $src)) {
        Write-Host "  $src не найдена — перемещать нечего" -ForegroundColor Yellow
        return
    }

    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    # /E — с подпапками, /MOVE — удалить источник, /IS /IT — перезаписывать одинаковые/изменённые
    & robocopy $src $dst /E /MOVE /IS /IT /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy $src -> $dst завершился с кодом $LASTEXITCODE" }
    $global:LASTEXITCODE = 0

    if (Test-Path $src) { Remove-Item $src -Recurse -Force }
    Write-Host "  перемещено -> $dst" -ForegroundColor Green
}

$results = [ordered]@{}

foreach ($ver in $Versions) {
    $config = "$Configuration R$($ver.ToString().Substring(2))"
    Write-Host ""
    Write-Host "===== Revit $ver ($config) =====" -ForegroundColor Cyan

    $running = Get-Process Revit -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -like "*Revit $ver*" }
    if ($running) {
        Write-Host "  Revit $ver запущен (PID $($running.Id -join ', ')) — пропуск, закройте Revit" -ForegroundColor Red
        $results["$ver"] = 'SKIPPED (Revit запущен)'
        continue
    }

    try {
        if (-not $SkipBuild) {
            Invoke-Build $pluginProj $config
            Invoke-Build $cmdProj    $config
        }
        Move-CommandSet $ver
        $results["$ver"] = 'OK'
    }
    catch {
        Write-Host "  ОШИБКА: $_" -ForegroundColor Red
        $results["$ver"] = "FAILED: $_"
    }
}

Write-Host ""
Write-Host "===== Итог =====" -ForegroundColor Cyan
foreach ($k in $results.Keys) {
    $color = if ($results[$k] -eq 'OK') { 'Green' } elseif ($results[$k] -like 'SKIPPED*') { 'Yellow' } else { 'Red' }
    Write-Host ("  {0}: {1}" -f $k, $results[$k]) -ForegroundColor $color
}

if ($results.Values | Where-Object { $_ -like 'FAILED*' }) { exit 1 }
