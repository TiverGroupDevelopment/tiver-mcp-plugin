using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>Таблицы подбора (lookup tables) семейства: список, импорт из CSV, экспорт, удаление.</summary>
public class ManageFamilyLookupTablesEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Manage family lookup tables";

    protected override object Run(UIApplication app, JObject args)
    {
        var action = (JsonArgs.OptionalString(args, "action") ?? "list").ToLowerInvariant();
        switch (action)
        {
            case "list":
                return Read(app, args, session => List(session.Document));

            case "export":
            {
                var name = JsonArgs.RequiredString(args, "name");
                var path = Path.GetFullPath(JsonArgs.RequiredString(args, "path"));
                var overwrite = JsonArgs.OptionalBool(args, "overwrite", false);
                return Read(app, args, session =>
                {
                    var manager = Manager(session.Document, false)
                                  ?? throw new InvalidOperationException("В семействе нет таблиц подбора");
                    if (!manager.GetAllSizeTableNames().Contains(name)) throw new ArgumentException($"Таблица '{name}' не найдена");
                    if (File.Exists(path) && !overwrite) throw new InvalidOperationException($"Файл '{path}' уже существует. Для замены передайте overwrite: true");
                    if (!manager.ExportSizeTable(name, path)) throw new InvalidOperationException($"Revit не экспортировал таблицу '{name}'");
                    return new { exported = name, path };
                });
            }

            case "import":
            {
                var path = Path.GetFullPath(JsonArgs.RequiredString(args, "path"));
                if (!File.Exists(path)) throw new ArgumentException($"Файл '{path}' не найден");
                return Edit(app, args, "MCP: import lookup table", doc =>
                {
                    var manager = Manager(doc, true);
                    var errorInfo = new FamilySizeTableErrorInfo();
                    if (!manager.ImportSizeTable(doc, path, errorInfo))
                    {
                        throw new InvalidOperationException(
                            $"Revit не импортировал '{path}': {errorInfo.FamilySizeTableErrorType}"
                            + (errorInfo.InvalidRowIndex >= 0 || errorInfo.InvalidColumnIndex >= 0
                                ? $" (строка {errorInfo.InvalidRowIndex}, столбец {errorInfo.InvalidColumnIndex})"
                                : string.Empty)
                            + (string.IsNullOrEmpty(errorInfo.InvalidHeaderText) ? string.Empty : $", заголовок '{errorInfo.InvalidHeaderText}'"));
                    }

                    return new { imported = Path.GetFileNameWithoutExtension(path), tables = List(doc) };
                });
            }

            case "remove":
            {
                var name = JsonArgs.RequiredString(args, "name");
                return Edit(app, args, "MCP: remove lookup table", doc =>
                {
                    var manager = Manager(doc, false)
                                  ?? throw new InvalidOperationException("В семействе нет таблиц подбора");
                    if (!manager.RemoveSizeTable(name)) throw new ArgumentException($"Таблица '{name}' не найдена или не удалена");
                    return new { removed = name, tables = List(doc) };
                });
            }

            default:
                throw new ArgumentException($"Неизвестное action '{action}'. Допустимо: list, import, export, remove");
        }
    }

    private static FamilySizeTableManager Manager(Document doc, bool create)
    {
        var familyId = doc.OwnerFamily?.Id ?? throw new InvalidOperationException("У документа нет семейства-владельца");
        var manager = FamilySizeTableManager.GetFamilySizeTableManager(doc, familyId);
        if (manager != null || !create) return manager;

        if (!FamilySizeTableManager.CreateFamilySizeTableManager(doc, familyId))
        {
            throw new InvalidOperationException("Revit не создал диспетчер таблиц подбора для этого семейства");
        }

        return FamilySizeTableManager.GetFamilySizeTableManager(doc, familyId);
    }

    private static object List(Document doc)
    {
        var manager = Manager(doc, false);
        if (manager == null) return new List<object>();

        return manager.GetAllSizeTableNames().Select(name =>
        {
            var table = manager.GetSizeTable(name);
            var headers = new List<string>();
            for (var column = 0; column < table.NumberOfColumns; column++)
            {
                headers.Add(table.GetColumnHeader(column).Name);
            }

            return (object)new { name, rows = table.NumberOfRows, columns = headers };
        }).ToList();
    }
}
