# Добавление команд в MCP-сервер для Revit

Oct 28, 2026 · @Sem

## Как устроено

Каждая команда проходит через три слоя, и её имя должно совпадать во всех трёх местах. Пути ниже указаны от `K:\tiver\mcp-servers-for-revit\mcp-servers-for-revit`.

| Слой | Папка | Что делает |
| --- | --- | --- |
| MCP-сервер (TypeScript) | `server/` | Объявляет инструмент для ИИ и пересылает вызов в Revit через `sendCommand` |
| Плагин Revit | `plugin/` | Читает `command.json`, находит в DLL класс с нужным `CommandName` через рефлексию |
| Набор команд (C#) | `commandset/` | Выполняет работу через Revit API |

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

Файл `commandset/Commands/<Папка>/MyCommand.cs`, образец: `Commands/Test/SayHelloCommand.cs`. Для семейств есть базовый класс `FamilyEditorCommandBase`, для топографии — `TopographyCommandBase`.

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
{ "commandName": "my_command", "description": "Что делает команда", "assemblyPath": "RevitMCPCommandSet.dll" }
```

### Шаг 4. MCP-инструмент (TypeScript)

Файл `server/src/tools/my_command.ts`, образец: `say_hello.ts`. Для семейств и топографии можно использовать помощник `registerFamilyTool`.

- Экспортировать функцию, имя которой начинается с `register…`.
- Внутри вызвать `server.tool("my_command", "<описание для ИИ>", { zod-схема с .describe() }, handler)`.
- В `handler` отправить команду: `withRevitConnection(c => c.sendCommand("my_command", args))`.
- Править `register.ts` не нужно: он сам подключает все файлы из `tools/`.
- Описание и `.describe()` писать подробно: по ним ИИ решает, когда и как вызывать инструмент.

### Шаг 5. Сборка

1. Сервер: `npm --prefix "K:/tiver/mcp-servers-for-revit/mcp-servers-for-revit/server" run build`.
2. Плагин и набор команд: собрать `mcp-servers-for-revit.sln` в Visual Studio. Конфигурации `Release R20`–`R24` для .NET 4.8, `R25`/`R26` для .NET 8. Результат появится в `plugin/bin/AddIn <год> <конфиг>/`.
3. При ручной установке скопировать DLL и `command.json` в `Commands/RevitMCPCommandSet/` папки плагина.

### Шаг 6. Подключение клиента к локальной сборке

Сделано: в `K:\tiver\mcp-servers-for-revit\.mcp.json` прописан сервер `revit-local`:

```json
{ "mcpServers": { "revit-local": { "type": "stdio", "command": "node",
    "args": ["K:/tiver/mcp-servers-for-revit/mcp-servers-for-revit/server/build/index.js"],
    "env": { "NODE_OPTIONS": "--dns-result-order=ipv4first" } } } }
```

Раньше клиент запускал `npx -y mcp-server-for-revit`, то есть опубликованный пакет из npm без новых команд. Старую запись нужно удалить, запустив из `C:\Users\sezav`:

```bash
claude mcp remove mcp-server-for-revit -s local
```

### Шаг 7. Перезапуск и проверка

- [ ] Перезапустить Revit, чтобы загрузились новый плагин и `command.json`.
- [ ] Открыть новую сессию Claude в `K:\tiver\mcp-servers-for-revit` и подтвердить доверие к `.mcp.json`.
- [ ] Проверить, что новый инструмент есть в списке (в терминале `claude` — команда `/mcp`).
- [ ] Вызвать инструмент на тестовой модели.

Если инструмент есть, но Revit отвечает «команда не найдена», проверить `command.json`, свежесть DLL и не выключена ли команда в настройках command set плагина.

## Известные проблемы

Локальный сервер отдаёт 40 инструментов из 43: три инструмента базы данных не загружаются.

| Проблема | Причина | Решение |
| --- | --- | --- |
| Не загружаются `store_project_data`, `store_room_data`, `query_stored_data` | `better-sqlite3` 12.8 требует Node 20+, установлен Node 18.0.0 | Обновить Node до LTS (22.x), затем `npm --prefix "K:/tiver/mcp-servers-for-revit/mcp-servers-for-revit/server" rebuild better-sqlite3` |
| Предупреждения «не найдена функция регистрации» при запуске | Пустые файлы `use_module.ts`, `search_modules.ts`, `modify_element.ts` в `server/src/tools` | Удалить или дописать; на работу не влияет |
| После правок в `server/src` новый инструмент не появился | Клиент запускает `server/build`, а не исходники | `npm run build` и новая сессия Claude |

## Возврат к исходным настройкам

Чтобы вернуть исходное состояние, достаточно удалить `revit-local` и вернуть запись через npx, если она уже удалена. Клиент снова будет запускать пакет из npm с 26 инструментами.

### Что было изменено

| Изменение | Нужен ли откат |
| --- | --- |
| Создан `K:\tiver\mcp-servers-for-revit\.mcp.json` с сервером `revit-local` | Да |
| Пересобран `server/build` из тех же исходников | Нет |
| Попытка пересобрать `better-sqlite3`, завершилась ошибкой, рабочее состояние не изменилось | Нет |
| Запись `mcp-server-for-revit` в `C:\Users\sezav\.claude.json` | Не трогалась; вернуть, только если вы её удалили |

### Как откатить

1. Удалить локальный сервер (или просто удалить файл `.mcp.json`):

   ```bash
   claude mcp remove revit-local -s project
   ```
2. Если доверие к `.mcp.json` уже подтверждено, сбросить его из `K:\tiver\mcp-servers-for-revit`:

   ```bash
   claude mcp reset-project-choices
   ```
3. Если старая запись через npx уже удалена, вернуть её из `C:\Users\sezav`. Исходные параметры: `"type": "stdio"`, `"command": "cmd"`, `"args": ["/c", "npx", "-y", "mcp-server-for-revit"]`.

   ```bash
   claude mcp add mcp-server-for-revit -s local -- cmd /c npx -y mcp-server-for-revit
   ```
4. Node: если вы обновили его и хотите вернуть 18.0.0, переустановить с nodejs.org (архив версий). Для отката MCP-конфига это не нужно.
5. Открыть новую сессию Claude и проверить, что сервер снова называется `mcp-server-for-revit`.
