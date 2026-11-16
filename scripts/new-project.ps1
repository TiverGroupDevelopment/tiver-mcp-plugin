<#
.SYNOPSIS
    Заводит новый проект моделирования семейств: профиль правил, папку скриптов и карточку проекта.

.DESCRIPTION
    Создаёт:
      rules\<Name>\profile.json            профиль правил проекта (наследует общие правила)
      rules\<Name>\правила\*.md            тексты правил по разделам
      projects\<Id>\README.md              карточка проекта: где что лежит
      projects\<Id>\scripts\README.md      папка скриптов проекта (ступень перед командой)
    Существующие файлы не перезаписываются. В конце запускает сверку scripts\check-integrity.mjs.

.EXAMPLE
    .\scripts\new-project.ps1 -Id proplan -Name "ПроПлан"
    .\scripts\new-project.ps1 -Id proplan -Name "ПроПлан" -Extends global,adsk -SharedParameterFile "D:\ФОП\ФОП2022.txt"
#>
param(
    # Короткий ключ проекта: латиница, цифры, дефис. Он же id профиля правил и "project" в манифестах команд.
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z][a-z0-9-]*$')]
    [string]$Id,

    # Название проекта: имя папки в rules\ и имя профиля.
    [Parameter(Mandatory)]
    [string]$Name,

    # Родительские профили правил, от общего к частному.
    [string[]]$Extends = @('global'),

    [ValidatePattern('^20\d\d$')]
    [string]$RevitVersion = '2019',

    # Файл общих параметров проекта: копируется в папку профиля.
    [string]$SharedParameterFile
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object Text.UTF8Encoding $false
$today = Get-Date -Format 'yyyy-MM-dd'

function Write-NewFile([string]$path, [string]$content) {
    if (Test-Path $path) {
        Write-Host "  уже есть, не трогаю: $path"
        return
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $path) | Out-Null
    [IO.File]::WriteAllText($path, $content, $utf8)
    Write-Host "  создан: $path"
}

# --- проверки ---
$rulesDir = Join-Path $root 'rules'
foreach ($existing in Get-ChildItem $rulesDir -Directory | Where-Object { $_.Name -notmatch '^[_.]' }) {
    $profilePath = Join-Path $existing.FullName 'profile.json'
    if (-not (Test-Path $profilePath)) { continue }
    $existingProfile = Get-Content $profilePath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($existingProfile.id -eq $Id -and $existing.Name -ne $Name) {
        throw "Профиль с id '$Id' уже есть: rules\$($existing.Name)"
    }
}
foreach ($parent in $Extends) {
    $found = Get-ChildItem $rulesDir -Directory | Where-Object {
        $p = Join-Path $_.FullName 'profile.json'
        (Test-Path $p) -and ((Get-Content $p -Raw -Encoding UTF8 | ConvertFrom-Json).id -eq $parent -or $_.Name -eq $parent)
    }
    if (-not $found) { throw "Родительский профиль '$parent' не найден в rules\" }
}
if ($SharedParameterFile -and -not (Test-Path $SharedParameterFile)) { throw "ФОП не найден: $SharedParameterFile" }

Write-Host "Проект '$Name' (id $Id)"

# --- профиль правил ---
$template = Join-Path $rulesDir '_шаблон'
$profileDir = Join-Path $rulesDir $Name
$profile = [IO.File]::ReadAllText((Join-Path $template 'profile.json'), $utf8)
$extendsJson = '[' + (($Extends | ForEach-Object { '"' + $_ + '"' }) -join ', ') + ']'
$profile = $profile.Replace('"project-id"', '"' + $Id + '"')
$profile = $profile.Replace('"Название проекта"', '"' + $Name.Replace('"', '\"') + '"')
$profile = $profile.Replace('"YYYY-MM-DD"', '"' + $today + '"')
$profile = $profile.Replace('"extends": ["global"]', '"extends": ' + $extendsJson)
$profile = $profile.Replace('"revitVersion": "2019"', '"revitVersion": "' + $RevitVersion + '"')
if ($SharedParameterFile) {
    $fopName = Split-Path -Leaf $SharedParameterFile
    $profile = $profile.Replace('"sharedParameterFile": "ФОП.txt"', '"sharedParameterFile": "' + $fopName + '"')
} else {
    # ФОП не задан: строка убирается, ФОП можно унаследовать от родителя или добавить позже.
    $profile = [regex]::Replace($profile, '\r?\n\s*"sharedParameterFile": "ФОП.txt",', '')
}
# У проекта с уже готовым профилем правила не трогаются: ему нужны только карточка и папка скриптов.
$profileIsNew = -not (Test-Path (Join-Path $profileDir 'profile.json'))
Write-NewFile (Join-Path $profileDir 'profile.json') $profile
if ($profileIsNew) {
    if ($SharedParameterFile) {
        $fopTarget = Join-Path $profileDir (Split-Path -Leaf $SharedParameterFile)
        if (-not (Test-Path $fopTarget)) {
            Copy-Item $SharedParameterFile $fopTarget
            Write-Host "  скопирован ФОП: $fopTarget"
        }
    }
    foreach ($file in Get-ChildItem (Join-Path $template 'правила') -Filter *.md) {
        $text = [IO.File]::ReadAllText($file.FullName, $utf8).Replace('«…»', "«$Name»").Replace('project-id', $Id).Replace('PRJ-', ($Id.ToUpper() + '-'))
        Write-NewFile (Join-Path $profileDir "правила\$($file.Name)") $text
    }
}

# --- карточка проекта и скрипты ---
$projectDir = Join-Path $root "projects\$Id"
$card = @"
# Проект «$Name» (``$Id``)

Заведён $today.

| Что | Где |
|---|---|
| Правила | [rules/$Name](../../rules/$Name/profile.json) — наследует: $($Extends -join ', ') |
| Скрипты без сборки | [scripts/](scripts/README.md) — запуск ``run_project_script`` |
| Набор команд | свой набор в ``commandSets`` корневого ``command.json`` (``"project": "$Id"``); в набор входят команды, у которых его имя есть в поле ``sets`` |
| Исходные данные и результаты | вне git |

Код команд общий (``commandset/``). Чтобы проект пользовался командой, имя его набора добавляется в ``sets`` команды. Новая команда только для проекта — ``"sets": ["<набор проекта>"]``, ``"status": "experimental"``.

## Правила проекта в работе агента

Правила проекта применяются, когда проект назван в задаче: агент вызывает ``get_family_rules profile: "$Id"`` и проверяет результат ``check_family_rules profile: "$Id"``. Без названного проекта действуют общие правила ``global``.

## Перенос в общие

Команда или правило проекта становится общим после проверки человеком: [PROMOTION.md](../../PROMOTION.md).
"@
Write-NewFile (Join-Path $projectDir 'README.md') $card

$scriptsReadme = @"
# Скрипты проекта «$Name»

Скрипт — C#-код для ``send_code_to_revit``, сохранённый в файл. Это первая ступень перед командой: сборка и перезапуск Revit не нужны.

- Список: ``list_project_scripts``
- Запуск: ``run_project_script name: "<имя>" parameters: [...]``

Файл ``<имя>.cs`` — тело метода ``Execute(Document document, object[] parameters)``. Шапка:

``````csharp
// description: что делает скрипт
// parameters: имя1, имя2
// transaction: auto
// usings: using Autodesk.Revit.DB.Plumbing;

var first = (string)parameters[0];
return document.Title + ": " + first;
``````

- ``parameters`` — имена по порядку: ``parameters[0]``, ``parameters[1]``.
- ``transaction`` — ``auto`` (скрипт идёт в транзакции) или ``none`` (скрипт сам управляет транзакциями).
- Дополнительные классы — в файле ``<имя>.classes.cs`` рядом.

Скрипт, который пригодился второй раз, — кандидат в команду проекта.
"@
Write-NewFile (Join-Path $projectDir 'scripts\README.md') $scriptsReadme

# --- сверка ---
Write-Host ""
$previousEncoding = [Console]::OutputEncoding
try {
    # node пишет в UTF-8: без этого кириллица в выводе сверки читается неверно.
    [Console]::OutputEncoding = [Text.Encoding]::UTF8
    & node (Join-Path $PSScriptRoot 'check-integrity.mjs') | ForEach-Object { Write-Host $_ }
} finally {
    [Console]::OutputEncoding = $previousEncoding
}
if ($LASTEXITCODE -ne 0) { throw "Сверка не пройдена — см. ошибки выше." }
