# Запуск MCP для нескольких Revit одновременно

Инструкция для случая, когда на одном компьютере открыто несколько Revit (например, 2019 и 2020) и каждым нужно управлять через MCP.

## Как это устроено

```
Claude ──► MCP-сервер "revit-local"   (REVIT_PORT=8080) ──► Revit 2020, порт 8080
       └─► MCP-сервер "revit-local-2" (REVIT_PORT=8081) ──► Revit 2019, порт 8081
```

- **Плагин в Revit** открывает TCP-порт. Сначала он пробует базовый порт из настроек (по умолчанию `8080`). Если порт занят другим Revit, плагин берёт следующий свободный: `8081`, `8082` и так до `8089`.
- **MCP-сервер** (`server/build/index.js`) подключается к одному Revit, адрес которого задают переменные окружения:
  - `REVIT_PORT` — порт, по умолчанию `8080`;
  - `REVIT_HOST` — хост, по умолчанию `localhost`.
- На каждый Revit регистрируется **свой** MCP-сервер со своим именем и своим `REVIT_PORT`. Инструменты в Claude получают префикс по имени сервера: `mcp__revit-local__...` и `mcp__revit-local-2__...`.

## 1. Установить плагин в каждую версию Revit

Плагин и набор команд собираются отдельно для каждой версии (конфигурации `Debug R19`, `Debug R20` и т. д.). Debug-сборка сама копирует файлы в `%AppData%\Autodesk\Revit\Addins\<год>\`. Перед сборкой закройте этот Revit, иначе DLL будут заблокированы.

```bash
dotnet build plugin -c "Debug R20"
```

```bash
dotnet build commandset -c "Debug R20"
```

Повторите для каждой версии (`Debug R19`, `Debug R21`, …).

> **Revit 2019.** В 2019 другие надстройки (FutureBIM, RevitPythonShell, CITRUS и др.) подгружают старые `System.Memory` / `System.Runtime.CompilerServices.Unsafe`, и компилятор Roslyn в `send_code_to_revit` не запускается. В этом случае плагин переключается на встроенный компилятор CodeDom, который поддерживает только **C# 5**: без `$"..."`, `?.`, `is X x`, кортежей и т. п.

## 2. Закрепить порт за каждой версией Revit

Если Revit запускать в разном порядке, автоматический выбор раздаст порты по-разному: первый запущенный получит `8080`, второй `8081`. Чтобы порт не зависел от порядка запуска, задайте базовый порт явно в настройках плагина каждой версии.

Файл настроек:

```
%AppData%\Autodesk\Revit\Addins\<год>\revit_mcp_plugin\Commands\commandRegistry.json
```

Добавьте в корень JSON блок `settings` (рядом с `"Commands"`):

```json
{
  "settings": {
    "port": 8081,
    "logLevel": "Info"
  },
  "Commands": [ ... ]
}
```

Например: Revit 2020 → `8080`, Revit 2019 → `8081`. Без блока `settings` используется `8080` и автоматический подбор.

> Кнопка **Save** в окне **Settings** на ленте сохраняет блок `settings` и другие ключи файла. В старых сборках плагина (до 10.11.2026) блок при сохранении пропадал: если после сохранения порта в файле нет, пересоберите `plugin` или добавьте блок заново.

## 3. Запустить Revit и включить MCP

1. Запустите каждый Revit.
2. На вкладке **mcp-servers-for-revit** нажмите кнопку включения сервиса. Появится окно `Open Server on port N`: это порт данного Revit.
3. Если окно не заметили, порт есть в журнале плагина:

   ```
   %AppData%\Autodesk\Revit\Addins\<год>\revit_mcp_plugin\Logs\mcp_<дата>.log
   ```

   Строка `Socket service listening on port 8081`. Перед ней может стоять `initialized on port 8080`: это базовый порт из настроек, а не фактический.

## 4. Зарегистрировать MCP-сервер на каждый порт

### Claude Code — файл `.mcp.json` в корне проекта

```json
{
  "mcpServers": {
    "revit-local": {
      "type": "stdio",
      "command": "node",
      "args": ["D:/Repos/tiver-mcp-plugin/server/build/index.js"],
      "env": { "NODE_OPTIONS": "--dns-result-order=ipv4first", "REVIT_PORT": "8080" }
    },
    "revit-local-2": {
      "type": "stdio",
      "command": "node",
      "args": ["D:/Repos/tiver-mcp-plugin/server/build/index.js"],
      "env": { "NODE_OPTIONS": "--dns-result-order=ipv4first", "REVIT_PORT": "8081" }
    }
  }
}
```

Сервер нужно один раз собрать (`npm install` и `npm run build` в папке `server`). Для третьего Revit добавьте `revit-local-3` с `REVIT_PORT=8082` и т. д.

Вместо локальной сборки можно использовать npm-пакет:

```json
"revit-2019": {
  "command": "cmd",
  "args": ["/c", "npx", "-y", "mcp-server-for-revit"],
  "env": { "REVIT_PORT": "8081" }
}
```

Или из терминала:

```bash
claude mcp add revit-2019 -e REVIT_PORT=8081 -- cmd /c npx -y mcp-server-for-revit
```

`NODE_OPTIONS=--dns-result-order=ipv4first` нужен, чтобы `localhost` разрешался в `127.0.0.1`: плагин слушает только IPv4.

### Claude Desktop — `claude_desktop_config.json`

Те же блоки в разделе `mcpServers`, затем перезапустите Claude Desktop.

## 5. Проверить подключение

Для проверки отправьте в каждый Revit через `send_code_to_revit` код, возвращающий его версию:

```csharp
return document.Application.VersionNumber + " | " + document.Title;
```

Ответ `2019 | ...` от `revit-local-2` и `2020 | ...` от `revit-local` означает, что порты сопоставлены правильно.

> Не используйте `say_hello` как проверку связи: эта команда открывает в Revit модальное окно, и пока его не закроют, остальные команды этого Revit зависают по таймауту.

## Частые проблемы

| Симптом | Причина и решение |
|---|---|
| Все запросы в один Revit уходят «не туда» | Revit запущены в другом порядке, и порты поменялись. Закрепите порты (шаг 2) или сверьте фактический порт по журналу. |
| `Failed to start server: no free port (8080-8089 busy?)` | Все 10 портов от базового заняты. Закройте лишние Revit или задайте другой базовый порт. |
| Таймаут на любую команду, в том числе короткую | В Revit открыто модальное окно (`say_hello`, предупреждение Revit, диалог надстройки). Закройте его. |
| `send_code_to_revit`: «Инициализатор типа PerTypeValues`1 выдал исключение» | Старая сборка набора команд в Revit 2019. Пересоберите `commandset` с `Debug R19` (шаг 1). |
| Длинный код (больше 8 КБ) зависает | Старая сборка плагина: запрос читался одним куском по 8 КБ. Пересоберите `plugin` для этой версии. |
| `Code execution timeout` | Код выполняется дольше 60 секунд. В Revit он продолжает работать: пишите результат в файл и читайте его после завершения. |
