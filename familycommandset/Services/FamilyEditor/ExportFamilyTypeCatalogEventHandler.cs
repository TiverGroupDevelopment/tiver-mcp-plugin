using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.Globalization;
using System.IO;
using System.Text;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Каталог типоразмеров: текстовый файл рядом с .rfa с тем же именем, из которого Revit предлагает
/// выбрать типы при загрузке семейства.
/// </summary>
public class ExportFamilyTypeCatalogEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Export family type catalog";

    protected override object Run(UIApplication app, JObject args)
    {
        var path = JsonArgs.OptionalString(args, "path");
        var overwrite = JsonArgs.OptionalBool(args, "overwrite", false);
        var only = JsonArgs.Has(args, "parameters")
            ? new HashSet<string>(JsonArgs.RequiredArray(args, "parameters").Select(t => t.Value<string>()), StringComparer.OrdinalIgnoreCase)
            : null;

        return Read(app, args, session =>
        {
            var doc = session.Document;
            var manager = doc.FamilyManager;
            var pinned = session.IsPinned ? FamilySessionRegistry.Current : null;

            string fullPath;
            if (path != null)
            {
                if (!path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("path должен оканчиваться на .txt");
                fullPath = Path.GetFullPath(path);
            }
            else
            {
                var familyPath = pinned != null && pinned.IsRelocated ? pinned.OriginalPath : doc.PathName;
                if (string.IsNullOrEmpty(familyPath))
                {
                    throw new ArgumentException("Семейство ещё не сохранено: укажите path (*.txt). Каталог должен лежать рядом с .rfa и называться так же.");
                }

                fullPath = Path.ChangeExtension(familyPath, ".txt");
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) throw new ArgumentException($"Папка '{directory}' не существует");
            if (File.Exists(fullPath) && !overwrite)
            {
                throw new InvalidOperationException($"Файл '{fullPath}' уже существует. Для замены передайте overwrite: true");
            }

            var types = FamilyParameterSpecs.AllTypes(manager).Where(t => !string.IsNullOrWhiteSpace(t.Name)).ToList();
            if (types.Count == 0) throw new InvalidOperationException("В семействе нет именованных типоразмеров (add_family_type)");

            var skipped = new List<object>();
            var columns = new List<(FamilyParameter parameter, FamilyValueKind kind, string header)>();
            foreach (var parameter in manager.Parameters.Cast<FamilyParameter>().OrderBy(p => p.Definition.Name))
            {
                var name = parameter.Definition.Name;
                if (only != null && !only.Contains(name)) continue;
                var kind = FamilyParameterSpecs.Classify(parameter);
                string reason = null;
                if (parameter.IsDeterminedByFormula) reason = "formula";
                else if (parameter.IsReporting) reason = "reporting";
                else if (kind == FamilyValueKind.Other) reason = "unsupported type";
                else if (!types.Any(t => t.HasValue(parameter))) reason = "no values";

                if (reason != null)
                {
                    if (only != null) skipped.Add(new { name, reason });
                    continue;
                }

                columns.Add((parameter, kind, name + UnitSuffix(kind)));
            }

            if (columns.Count == 0) throw new InvalidOperationException("Нет параметров, пригодных для каталога типоразмеров");

            var text = new StringBuilder();
            text.Append(',').AppendLine(string.Join(",", columns.Select(c => Escape(c.header))));
            foreach (var type in types)
            {
                text.Append(Escape(type.Name));
                foreach (var column in columns)
                {
                    text.Append(',').Append(Format(FamilyParameterSpecs.ReadValue(doc, type, column.parameter, column.kind)));
                }

                text.AppendLine();
            }

            // UTF-16 с BOM: Revit читает такой каталог без потери кириллицы.
            File.WriteAllText(fullPath, text.ToString(), Encoding.Unicode);

            return new
            {
                path = fullPath,
                types = types.Count,
                columns = columns.Select(c => c.header).ToList(),
                skipped
            };
        });
    }

    private static string UnitSuffix(FamilyValueKind kind)
    {
        switch (kind)
        {
            case FamilyValueKind.Length: return "##length##millimeters";
            case FamilyValueKind.Angle: return "##angle##degrees";
            case FamilyValueKind.Area: return "##area##square_meters";
            case FamilyValueKind.Volume: return "##volume##cubic_meters";
            case FamilyValueKind.Number: return "##number##general";
            default: return "##other##";
        }
    }

    private static string Format(object value)
    {
        switch (value)
        {
            case null: return string.Empty;
            case bool flag: return flag ? "1" : "0";
            case double number: return number.ToString("0.######", CultureInfo.InvariantCulture);
            case IFormattable formattable: return formattable.ToString(null, CultureInfo.InvariantCulture);
            default: return Escape(value.ToString());
        }
    }

    private static string Escape(string text)
    {
        if (text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
