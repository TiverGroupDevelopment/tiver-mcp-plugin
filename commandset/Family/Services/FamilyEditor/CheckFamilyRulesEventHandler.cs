using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Проверка семейства по набору правил (checks из профиля правил проекта). Семейство — файл .rfa (path, открывается
/// в фоне и закрывается без сохранения) или текущее: закреплённая сессия, активный документ, familyName из проекта.
/// Только чтение.
/// </summary>
public class CheckFamilyRulesEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Check family rules";

    protected override object Run(UIApplication app, JObject args)
    {
        var checks = JsonArgs.RequiredObject(args, "checks");
        var path = JsonArgs.OptionalString(args, "path");
        if (path == null)
        {
            return Read(app, args, session => Report(session.Document, checks, null));
        }

        var fullPath = System.IO.Path.GetFullPath(path);
        if (!System.IO.File.Exists(fullPath)) throw new ArgumentException($"Файл '{fullPath}' не найден");
        var open = app.Application.Documents.Cast<Document>()
            .FirstOrDefault(d => d.IsFamilyDocument && string.Equals(d.PathName, fullPath, StringComparison.OrdinalIgnoreCase));
        if (open != null) return Report(open, checks, fullPath);

        Document doc;
        try
        {
            doc = app.Application.OpenDocumentFile(fullPath);
        }
        catch (Exception ex) when (ex.GetType().Name == "CorruptModelException" || ex.Message.Contains("later version"))
        {
            throw new InvalidOperationException($"Файл сохранён в более новой версии Revit, чем {app.Application.VersionNumber}: проверьте его в той версии, где он создан", ex);
        }

        try
        {
            return Report(doc, checks, fullPath);
        }
        finally
        {
            doc.Close(false);
        }
    }

    private static object Report(Document doc, JObject checks, string path)
    {
        if (!doc.IsFamilyDocument) throw new InvalidOperationException($"'{doc.Title}' не семейство");
        var results = FamilyRuleChecker.Run(doc, checks);
        return new
        {
            family = FamilySessionRegistry.StripExtension(doc.Title),
            path = path ?? (string.IsNullOrEmpty(doc.PathName) ? null : doc.PathName),
            summary = new
            {
                fail = results.Count(r => r.Status == "fail"),
                warn = results.Count(r => r.Status == "warn"),
                pass = results.Count(r => r.Status == "pass"),
                skip = results.Count(r => r.Status == "skip")
            },
            results = results.Select(r => r.Describe()).ToList()
        };
    }
}
