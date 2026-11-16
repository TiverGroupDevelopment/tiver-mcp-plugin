using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Пакетная выгрузка таблиц выбора (lookup tables) в CSV из семейств папки или списка файлов.
/// Семейства открываются в фоне и закрываются без сохранения; уже открытые читаются как есть. Только чтение .rfa.
/// </summary>
public class ExportFamilyLookupTablesEventHandler : FamilyEditorEventHandlerBase
{
    private static readonly Regex Backup = new Regex(@"\.\d{4}\.rfa$", RegexOptions.IgnoreCase);

    public override string GetName() => "Export family lookup tables";

    protected override object Run(UIApplication app, JObject args)
    {
        var folder = JsonArgs.OptionalString(args, "folder");
        var files = new List<string>();
        if (folder != null)
        {
            folder = Path.GetFullPath(folder);
            if (!Directory.Exists(folder)) throw new ArgumentException($"Папка '{folder}' не найдена");
            var option = JsonArgs.OptionalBool(args, "recursive", false) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            files.AddRange(Directory.GetFiles(folder, "*.rfa", option));
        }

        if (JsonArgs.Has(args, "paths"))
        {
            foreach (var token in JsonArgs.RequiredArray(args, "paths"))
            {
                var path = Path.GetFullPath(token.Value<string>());
                if (!File.Exists(path)) throw new ArgumentException($"Файл '{path}' не найден");
                files.Add(path);
            }
        }

        if (folder == null && files.Count == 0) throw new ArgumentException("Задайте folder (папка с .rfa) и/или paths (список .rfa)");

        var exclude = JsonArgs.Has(args, "exclude")
            ? JsonArgs.RequiredArray(args, "exclude").Select(t => t.Value<string>()).Where(s => !string.IsNullOrEmpty(s)).ToList()
            : new List<string>();
        files = files
            .Where(f => !Backup.IsMatch(f))
            .Where(f => !exclude.Any(x => Path.GetFileName(f).IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var outDirArg = JsonArgs.OptionalString(args, "outDir");
        var outDir = outDirArg != null ? Path.GetFullPath(outDirArg) : null;
        if (outDir == null && folder != null) outDir = Path.Combine(folder, "таблицы_выбора");
        var overwrite = JsonArgs.OptionalBool(args, "overwrite", true);
        var encodingName = (JsonArgs.OptionalString(args, "encoding") ?? "cp1251").ToLowerInvariant();
        var encoding = ResolveEncoding(encodingName);

        var exported = new List<object>();
        var skipped = new List<object>();
        foreach (var file in files)
        {
            var target = outDir ?? Path.Combine(Path.GetDirectoryName(file) ?? string.Empty, "таблицы_выбора");
            Directory.CreateDirectory(target);
            var open = app.Application.Documents.Cast<Document>()
                .FirstOrDefault(d => d.IsFamilyDocument && string.Equals(d.PathName, file, StringComparison.OrdinalIgnoreCase));
            Document doc = open;
            try
            {
                doc = doc ?? app.Application.OpenDocumentFile(file);
                var manager = FamilySizeTableManager.GetFamilySizeTableManager(doc, doc.OwnerFamily.Id);
                var names = manager?.GetAllSizeTableNames()?.ToList() ?? new List<string>();
                if (names.Count == 0)
                {
                    skipped.Add(new { file, reason = "таблиц выбора нет" });
                    continue;
                }

                foreach (var name in names)
                {
                    var csv = Path.Combine(target, Safe(name) + ".csv");
                    if (File.Exists(csv) && !overwrite)
                    {
                        skipped.Add(new { file, table = name, reason = $"'{csv}' уже есть (overwrite: false)" });
                        continue;
                    }

                    var temp = Path.Combine(Path.GetTempPath(), "mcp_lookup_" + Guid.NewGuid().ToString("N") + ".csv");
                    try
                    {
                        if (!manager.ExportSizeTable(name, temp)) throw new InvalidOperationException($"Revit не выгрузил таблицу '{name}'");
                        var table = manager.GetSizeTable(name);
                        var text = Rebuild(Decode(File.ReadAllBytes(temp)), table, doc);
                        File.WriteAllText(csv, text, encoding);
                        exported.Add(new { file, table = name, csv, rows = table.NumberOfRows, columns = table.NumberOfColumns });
                    }
                    finally
                    {
                        if (File.Exists(temp)) File.Delete(temp);
                    }
                }
            }
            catch (Exception ex)
            {
                skipped.Add(new { file, reason = ex.Message });
            }
            finally
            {
                if (open == null && doc != null) doc.Close(false);
            }
        }

        return new
        {
            files = files.Count,
            outDir,
            encoding = encodingName,
            exported,
            skipped
        };
    }

    private static Encoding Cp1251()
    {
#if NET
        // В .NET 8 (Revit 2025+) кодовые страницы Windows подключаются отдельно.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
        return Encoding.GetEncoding(1251);
    }

    private static Encoding ResolveEncoding(string name)
    {
        switch (name)
        {
            case "cp1251":
            case "ansi":
            case "windows-1251":
                return Cp1251();
            case "utf8":
                return new UTF8Encoding(false);
            case "utf8bom":
                return new UTF8Encoding(true);
            default:
                throw new ArgumentException($"Неизвестная кодировка '{name}'. Допустимо: cp1251, utf8, utf8bom");
        }
    }

    /// <summary>
    /// Revit 2019 в ExportSizeTable выбрасывает не-ASCII символы: «C--TB Муфта соединительная 20» → «C--TB »,
    /// «L_общ» → «L_». Поэтому из выгрузки Revit берутся только типы столбцов (часть заголовка после «##»),
    /// а имена столбцов и значения — из API таблицы. Числа без хвостовых нулей («20», «52.85»), как в исходном CSV.
    /// </summary>
    private static string Rebuild(string exported, FamilySizeTable table, Document doc)
    {
        var lines = exported.Replace("\r\n", "\n").Split('\n');
        var headers = lines.Length > 0 ? lines[0].Split(',') : new string[0];
        if (headers.Length != table.NumberOfColumns) return exported;

        var builder = new StringBuilder();
        var numeric = new bool[table.NumberOfColumns];
        var header = new List<string>();
        for (var column = 0; column < table.NumberOfColumns; column++)
        {
            var name = table.GetColumnHeader(column).Name;
            var hash = headers[column].IndexOf("##", StringComparison.Ordinal);
            // У заголовков с кириллицей Revit 2019 теряет и тип («L_общ##length##millimeters» → «L_»): берём его из параметра семейства.
            var suffix = hash >= 0 ? headers[column].Substring(hash) : column == 0 ? string.Empty : SuffixOf(doc, name);
            numeric[column] = column > 0 && suffix.Length > 0 && !suffix.StartsWith("##other", StringComparison.OrdinalIgnoreCase);
            header.Add(Quote(name + suffix));
        }

        builder.Append(string.Join(",", header)).Append("\r\n");
        for (var row = 0; row < table.NumberOfRows; row++)
        {
            var cells = new List<string>();
            for (var column = 0; column < table.NumberOfColumns; column++)
            {
                var value = table.AsValueString(row, column) ?? string.Empty;
                if (numeric[column] && value.IndexOf('.') >= 0 && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    value = value.TrimEnd('0').TrimEnd('.');
                }

                cells.Add(Quote(value));
            }

            builder.Append(string.Join(",", cells)).Append("\r\n");
        }

        return builder.ToString();
    }

    private static string SuffixOf(Document doc, string name)
    {
        var parameter = doc.FamilyManager.Parameters.Cast<FamilyParameter>()
            .FirstOrDefault(p => string.Equals(p.Definition.Name, name, StringComparison.Ordinal));
        if (parameter == null) return "##other##";
        var kind = FamilyParameterSpecs.Classify(parameter);
        return kind == FamilyValueKind.Integer ? "##number##general" : ExportFamilyTypeCatalogEventHandler.UnitSuffix(kind);
    }

    private static string Quote(string value)
    {
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Revit пишет CSV в UTF-8 (с BOM или без) либо в ANSI — определяем по байтам.</summary>
    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Cp1251().GetString(bytes);
        }
    }

    private static string Safe(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }
}
