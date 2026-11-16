# Скрипты проекта «ПроПроект»

Скрипт — C#-код для `send_code_to_revit`, сохранённый в файл. Это первая ступень перед командой: сборка и перезапуск Revit не нужны.

- Список: `list_project_scripts`
- Запуск: `run_project_script name: "<имя>" parameters: [...]`

Файл `<имя>.cs` — тело метода `Execute(Document document, object[] parameters)`. Шапка:

```csharp
// description: что делает скрипт
// parameters: имя1, имя2
// transaction: auto
// usings: using Autodesk.Revit.DB.Plumbing;

var first = (string)parameters[0];
return document.Title + ": " + first;
```

- `parameters` — имена по порядку: `parameters[0]`, `parameters[1]`.
- `transaction` — `auto` (скрипт идёт в транзакции) или `none` (скрипт сам управляет транзакциями).
- Дополнительные классы — в файле `<имя>.classes.cs` рядом.

Скрипт, который пригодился второй раз, — кандидат в команду проекта.