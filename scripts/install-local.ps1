<#
.SYNOPSIS
    Устанавливает собранный плагин и оба набора команд (RevitMCPCommandSet, RevitMCPFamilyCommandSet)
    в папку надстроек Revit и обновляет реестр команд плагина.

.DESCRIPTION
    Берёт раскладку из plugin\bin\AddIn <год> <конфигурация>\ (её собирает mcp-servers-for-revit.sln),
    копирует её в %APPDATA%\Autodesk\Revit\Addins\<год>\ и пересобирает
    revit_mcp_plugin\Commands\commandRegistry.json по command.json обоих наборов.
    Сохраняются блок settings (порт и т.п.) и флаги enabled уже известных команд;
    новые команды включаются. Перед копированием делается резервная копия прежней установки.
    Revit этой версии должен быть закрыт: иначе DLL заблокированы.

.EXAMPLE
    .\scripts\install-local.ps1 -Year 2019
    .\scripts\install-local.ps1 -Year 2026 -Port 8081
#>
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^20\d\d$')]
    [string]$Year,

    [string]$Configuration,

    # Закрепить порт плагина (иначе 8080 и автоподбор следующего свободного).
    [int]$Port = 0
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Configuration) { $Configuration = "Release R$($Year.Substring(2))" }

$source = Join-Path $root "plugin\bin\AddIn $Year $Configuration"
if (-not (Test-Path (Join-Path $source 'revit_mcp_plugin\RevitMCPPlugin.dll'))) {
    throw "Сборка не найдена: $source. Соберите решение: dotnet build mcp-servers-for-revit.sln -c `"$Configuration`""
}

$running = Get-Process Revit -ErrorAction SilentlyContinue | Where-Object { $_.Path -match "Revit $Year" }
if ($running) { throw "Revit $Year запущен (PID $($running.Id -join ', ')). Закройте его и повторите." }

$addins = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$Year"
New-Item -ItemType Directory -Force $addins | Out-Null
$target = Join-Path $addins 'revit_mcp_plugin'
$registryPath = Join-Path $target 'Commands\commandRegistry.json'

# Резервная копия прежней установки (без логов).
$oldRegistry = $null
if (Test-Path $target) {
    $backup = Join-Path $env:USERPROFILE ("Documents\revit-mcp-backup\{0}-{1}" -f $Year, (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force $backup | Out-Null
    Copy-Item -Recurse -Force $target (Join-Path $backup 'revit_mcp_plugin') -Exclude 'Logs'
    if (Test-Path $registryPath) { $oldRegistry = Get-Content $registryPath -Raw -Encoding UTF8 | ConvertFrom-Json }
    Write-Host "Резервная копия: $backup"
}

Copy-Item -Recurse -Force (Join-Path $source 'revit_mcp_plugin') $addins
$addinFile = Join-Path $addins 'mcp-servers-for-revit.addin'
if (-not (Test-Path $addinFile)) { Copy-Item (Join-Path $source 'mcp-servers-for-revit.addin') $addins }

# Реестр команд: все команды обоих наборов, флаги enabled — из прежнего реестра.
$enabled = @{}
if ($oldRegistry -and $oldRegistry.Commands) {
    foreach ($c in $oldRegistry.Commands) { $enabled[$c.commandName] = [bool]$c.enabled }
}

$commands = New-Object System.Collections.ArrayList
foreach ($set in Get-ChildItem (Join-Path $target 'Commands') -Directory) {
    $manifestPath = Join-Path $set.FullName 'command.json'
    if (-not (Test-Path $manifestPath)) { continue }
    $manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not (Test-Path (Join-Path $set.FullName $Year))) { continue }
    foreach ($c in $manifest.commands) {
        $isEnabled = if ($enabled.ContainsKey($c.commandName)) { $enabled[$c.commandName] } else { $true }
        [void]$commands.Add([ordered]@{
            commandName            = $c.commandName
            assemblyPath           = "$($manifest.name)\{VERSION}\$($c.assemblyPath)"
            enabled                = $isEnabled
            supportedRevitVersions = @($Year)
            developer              = $manifest.developer
            description            = $c.description
        })
    }
}

$registry = [ordered]@{}
if ($oldRegistry -and $oldRegistry.settings) { $registry.settings = $oldRegistry.settings }
if ($Port -gt 0) {
    if (-not $registry.settings) { $registry.settings = [ordered]@{} }
    $registry.settings | Add-Member -NotePropertyName port -NotePropertyValue $Port -Force -ErrorAction SilentlyContinue
    if ($registry.settings -is [System.Collections.IDictionary]) { $registry.settings['port'] = $Port }
}
$registry.Commands = $commands

$json = $registry | ConvertTo-Json -Depth 10
[IO.File]::WriteAllText($registryPath, $json, (New-Object Text.UTF8Encoding $false))

$bySet = $commands | Group-Object { $_.assemblyPath.Split('\')[0] } | ForEach-Object { "$($_.Name): $($_.Count)" }
Write-Host "Установлено в $addins"
Write-Host ("Команд в реестре: {0} ({1})" -f $commands.Count, ($bySet -join ', '))
$disabled = @($commands | Where-Object { -not $_.enabled } | ForEach-Object { $_.commandName })
if ($disabled.Count -gt 0) { Write-Host "Выключены (как было): $($disabled -join ', ')" }
