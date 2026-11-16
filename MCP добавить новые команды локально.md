# Добавление команд в MCP-сервер для Revit

Oct 28, 2026 · @Sem

## Как устроено

Каждая команда проходит через три слоя, и её имя должно совпадать во всех трёх местах. Пути ниже указаны от корня репозитория.

| Слой | Папка | Что делает |
| --- | --- | --- |
| MCP-сервер (TypeScript) | `server/` | Объявляет инструмент для ИИ и пересылает вызов в Revit через `sendCommand` |
| Плагин Revit | `plugin/` | Читает `command.json`, находит в DLL класс с нужным `CommandName` через рефлексию |
| Команды (C#) | `commandset/` | Код всех команд: модель проекта и редактор семейств (`commandset/Family/`), одна сборка `RevitMCPCommandSet.dll` |

Код всех команд лежит в одном месте, манифест — корневой `command.json`. Наборы (Global, Alterplast) — это списки: к каким наборам относится команда, говорит её поле `sets`. Команда может входить в несколько наборов. Новая команда входит в Global, если пригодится при моделировании вообще, и в набор проекта, если проект ею пользуется ([PROMOTION.md](PROMOTION.md)). Утилиты `JsonArgs`, `FamilyEditorIds`, `WarningCollector` есть в двух пространствах имён: старое (`RevitMCPCommandSet.Utils.FamilyEditor`) нужно топографии.

Имя команды должно совпадать в трёх местах: `CommandName` в C#, `commandName` в `command.json` и `sendCommand("…")` в TypeScript. Имя инструмента для ИИ может отличаться: например, `color_elements` вызывает команду `color_splash`.

## Как добавить команду

Новая команда — это четыре правки в коде и пересборка обеих частей. В примерах команда называется `my_command`.

### Шаг 1. Обработчик (C#)

Файл `commandset/Services/MyCommandEventHandler.cs`, образец: `SayHelloEventHandler.cs`.

- Реализует `IExternalEventHandler` и `IWaitableExternalEventHandler`.
- Входные данные и результат передаются через свойства.
- В `Execute(UIApplication app)` выполняется работа с Revit API. Если модель меняется, нужна `Transaction`.
- В `finally` обязательно вызвать `_resetEvent.Set()`, иначе команда будет ждать до таймаута.

### Шаг 2. Команда (C#)

Файл `commandset/Commands/<Папка>/MyCommand.cs`, образец: `Commands/Test/SayHelloCommand.cs`. Для семейств есть базовый класс `FamilyEditorCommandBase` (в `commandset/Family/Commands/FamilyEditor/`), для топографии — `TopographyCommandBase`.

```csharp
public class MyCommand : ExternalEventCommandBase
{
    private MyCommandEventHandler _handler => (MyCommandEventHandler)Handler;
    public override string CommandName => "my_command";

    public MyCommand(UIApplication uiApp) : base(new MyCommandEventHandler(), uiApp) { }

    public override object Execute(JObject parameters, string requestId)
    {
        _handler.SomeParam = parameters?["someParam"]?.ToString();
        if (RaiseAndWaitForCompletion(15000))
            return _handler.Result;
        throw new TimeoutException("my_command timed out");
    }
}
```

Регистрировать класс отдельно не нужно: плагин находит его через рефлексию.

### Шаг 3. Манифест command.json

Добавить запись в массив `commands`:

```json
{
  "commandName": "my_command",
  "description": "Что делает команда",
  "assemblyPath": "RevitMCPCommandSet.dll",
  "section": "geometry",
  "status": "experimental",
  "sets": ["Global", "Alterplast"]
}
```

Три метки обязательны:

| Метка | Значения |
| --- | --- |
| `section` | раздел из `sections.json`: `session`, `parameters`, `geometry`, `nested`, `graphics`, `mep`, `checks`, `service`, `project` |
| `status` | `experimental` — новая, не проверена; `validated` — проверена |
| `sets` | наборы, в которые входит команда: `["Global"]` — общая; `["Global", "Alterplast"]` — общая, которой пользуется проект; `["Alterplast"]` — только для проекта. Наборы описаны в `commandSets` того же файла. Агенту доступны все команды |

Если имя инструмента для ИИ отличается от имени команды, добавить `"tool": "<имя инструмента>"`. Инструмент, у которого нет своей C#-команды, записывается в `server/command.json`.

Имя команды уникально: плагин не загрузит вторую команду с тем же именем и запишет ошибку в лог. Набор — не копия кода: одна команда в двух наборах — одна запись с двумя именами в `sets`.

Как команда проекта становится общей (добавить `"Global"` в `sets`) — в [PROMOTION.md](PROMOTION.md).

### Шаг 4. MCP-инструмент (TypeScript)

Файл `server/src/tools/my_command.ts`, образец: `say_hello.ts`. Для семейств и топографии можно использовать помощник `registerFamilyTool`.

- Экспортировать функцию, имя которой начинается с `register…`.
- Внутри вызвать `server.tool("my_command", "<описание для ИИ>", { zod-схема с .describe() }, handler)`.
- В `handler` отправить команду: `withRevitConnection(c => c.sendCommand("my_command", args))`.
- Править `register.ts` не нужно: он сам подключает все файлы из `tools/`.
- Описание и `.describe()` писать подробно: по ним ИИ решает, когда и как вызывать инструмент.

### Шаг 5. Сборка

0. Сверка трёх слоёв: `node scripts/check-integrity.mjs`. Скрипт находит несовпадение имён между манифестом, C# и инструментом, дубли имён между наборами и пропущенные метки. Запускать до сборки.
1. Сервер (из корня репозитория): `npm --prefix server run build`.
1a. Параметры команд в манифестах: `node scripts/sync-command-parameters.mjs`. Скрипт берёт входные параметры из схем инструментов собранного сервера и пишет их в поле `parameters` (`--check` — только проверить). Их показывает столбец Parameters в окне настроек плагина. Описание команды (`description`) в манифесте пишется по-русски: его видит пользователь в том же окне.
2. Плагин и набор команд: собрать `tiver-mcp-plugin.sln` в Visual Studio или `dotnet build tiver-mcp-plugin.sln -c "Release R19"`. Конфигурации `Release R20`–`R24` для .NET 4.8, `R25`/`R26` для .NET 8. Результат появится в `plugin/bin/AddIn <год> <конфиг>/`.
3. Установка: `.\scripts\install-local.ps1 -Year <год>` — копирует сборку команд в `Commands/RevitMCPCommandSet/`, убирает устаревшие папки и пересобирает реестр команд. При ручной установке скопировать DLL и `command.json` в эту папку, затем в Revit открыть **Settings** и сохранить.

### Шаг 6. Подключение клиента к локальной сборке

Сделано: в `.mcp.json` в корне репозитория прописан сервер `revit-local`:

```json
{ "mcpServers": { "revit-local": { "type": "stdio", "command": "node",
    "args": ["${CLAUDE_PROJECT_DIR:-.}/server/build/index.js"],
    "env": { "NODE_OPTIONS": "--dns-result-order=ipv4first" } } } }
```

Путь к серверу не абсолютный: файл лежит в git и работает в любой папке, куда склонирован репозиторий. Форма `${CLAUDE_PROJECT_DIR:-.}` — та, что документация Claude Code советует для `args` в `.mcp.json` проекта. После изменения `.mcp.json` Claude Code заново спрашивает разрешение на сервер `revit-local`.

Папку исходных данных проектов (для эталонов в профилях правил) каждый задаёт у себя переменной `REVIT_MCP_DATA_DIR`. Например, в `.claude/settings.local.json` (этот файл не попадает в git):

```json
{ "env": { "REVIT_MCP_DATA_DIR": "D:\\Семейства" } }
```

### Шаг 7. Перезапуск и проверка

- [ ] Перезапустить Revit, чтобы загрузились новый плагин и `command.json`.
- [ ] Открыть новую сессию Claude в корне репозитория и подтвердить доверие к `.mcp.json`.
- [ ] Проверить, что новый инструмент есть в списке (в терминале `claude` — команда `/mcp`).
- [ ] Вызвать инструмент на тестовой модели.

Если инструмент есть, но Revit отвечает «команда не найдена», проверить `command.json`, свежесть DLL и не выключена ли команда в настройках command set плагина.

## Известные проблемы

Локальный сервер отдаёт 40 инструментов из 43: три инструмента базы данных не загружаются.

| Проблема | Причина | Решение |
| --- | --- | --- |
| Не загружаются `store_project_data`, `store_room_data`, `query_stored_data` | `better-sqlite3` 12.8 требует Node 20+, установлен Node 18.0.0 | Обновить Node до LTS (22.x), затем из корня репозитория `npm --prefix server rebuild better-sqlite3` |
| Предупреждения «не найдена функция регистрации» при запуске | Пустые файлы `use_module.ts`, `search_modules.ts`, `modify_element.ts` в `server/src/tools` | Удалить или дописать; на работу не влияет |
| После правок в `server/src` новый инструмент не появился | Клиент запускает `server/build`, а не исходники | `npm run build` и новая сессия Claude |
