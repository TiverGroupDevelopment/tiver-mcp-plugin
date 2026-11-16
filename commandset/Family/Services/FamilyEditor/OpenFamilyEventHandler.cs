using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class OpenFamilyEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Open family";

    protected override object Run(UIApplication app, JObject args)
    {
        var existing = FamilySessionRegistry.Current;
        if (existing != null)
        {
            throw new InvalidOperationException($"Уже открыта сессия семейства '{existing.FamilyName}'. Сначала вызовите close_family.");
        }

        var path = JsonArgs.OptionalString(args, "path");
        var familyName = JsonArgs.OptionalString(args, "familyName");
        if (path != null && familyName != null)
        {
            throw new ArgumentException("Передайте либо path (.rfa на диске), либо familyName (семейство из активного проекта), но не оба");
        }

        PinnedFamilySession session;
        if (path != null)
        {
            session = OpenFile(app, path);
        }
        else
        {
            session = OpenFromActiveDocument(app, familyName);
        }

        return new
        {
            session = session.Describe(),
            document = FamilyDocInfo.Summarize(session.Document),
            warnings = Warnings
        };
    }

    private static PinnedFamilySession OpenFile(UIApplication app, string path)
    {
        if (!path.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("path должен оканчиваться на .rfa");
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new ArgumentException($"Файл '{fullPath}' не найден");

        var alreadyOpen = app.Application.Documents
            .Cast<Document>()
            .FirstOrDefault(d => d.IsFamilyDocument && string.Equals(d.PathName, fullPath, StringComparison.OrdinalIgnoreCase));

        var doc = alreadyOpen ?? app.Application.OpenDocumentFile(fullPath)
                  ?? throw new InvalidOperationException($"Revit не открыл '{fullPath}'");

        if (!doc.IsFamilyDocument)
        {
            if (alreadyOpen == null) doc.Close(false);
            throw new ArgumentException($"'{fullPath}' не является семейством");
        }

        return FamilySessionRegistry.Pin(doc, null, "file");
    }

    private static PinnedFamilySession OpenFromActiveDocument(UIApplication app, string familyName)
    {
        var active = app.ActiveUIDocument?.Document
                     ?? throw new InvalidOperationException("В Revit нет активного документа: передайте path к .rfa или создайте семейство через new_family");

        if (active.IsFamilyDocument)
        {
            var title = FamilySessionRegistry.StripExtension(active.Title);
            if (familyName != null && !string.Equals(title, familyName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Активен документ семейства '{title}', а запрошено '{familyName}'. Активируйте проект или передайте path.");
            }

            return FamilySessionRegistry.Pin(active, null, "active");
        }

        if (familyName == null)
        {
            throw new ArgumentException("Активен проект: укажите familyName загруженного семейства или path к .rfa");
        }

        var family = FamilyEditSession.FindLoadedFamily(active, familyName);
        var doc = active.EditFamily(family);
        return FamilySessionRegistry.Pin(doc, active, "project", family.Name);
    }
}
