using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Services.ProjectModel;

/// <summary>
/// Делает вид активным в документе (проект или семейство) и закрывает остальные открытые виды этого документа.
/// Документ: по имени (переключается на него, если он открыт из файла), иначе активный.
/// </summary>
public class SetOpenViewsEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Set open views";

    protected override object Run(UIApplication app, JObject args)
    {
        var title = JsonArgs.OptionalString(args, "document");
        var viewName = JsonArgs.OptionalString(args, "view");
        var viewKind = (JsonArgs.OptionalString(args, "viewType") ?? "3d").ToLowerInvariant();
        var closeOthers = JsonArgs.OptionalBool(args, "closeOthers", true);

        var uiDoc = app.ActiveUIDocument ?? throw new InvalidOperationException("В Revit нет активного документа");
        if (title != null && !Matches(uiDoc.Document, title))
        {
            var doc = app.Application.Documents.Cast<Document>().FirstOrDefault(d => Matches(d, title))
                      ?? throw new ArgumentException($"Документ '{title}' не открыт. Открыты: {string.Join(", ", app.Application.Documents.Cast<Document>().Select(d => d.Title))}");
            if (string.IsNullOrEmpty(doc.PathName)) throw new InvalidOperationException($"Документ '{doc.Title}' не сохранён в файл: переключитесь на него вручную");
            uiDoc = app.OpenAndActivateDocument(doc.PathName);
        }

        var document = uiDoc.Document;
        var views = new FilteredElementCollector(document).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && v.CanBePrinted || v is View3D && !((View3D)v).IsTemplate).ToList();
        View target;
        if (viewName != null)
        {
            target = views.FirstOrDefault(v => string.Equals(v.Name, viewName, StringComparison.OrdinalIgnoreCase))
                     ?? throw new ArgumentException($"Вид '{viewName}' не найден. Виды: {string.Join(", ", views.Select(v => v.Name).Distinct().Take(40))}");
        }
        else if (viewKind == "3d")
        {
            target = views.OfType<View3D>().FirstOrDefault(v => !v.IsTemplate)
                     ?? throw new ArgumentException($"В документе '{document.Title}' нет 3D-вида (у семейств профилей его не бывает)");
        }
        else
        {
            target = uiDoc.ActiveView;
        }

        if (uiDoc.ActiveView.Id != target.Id) uiDoc.ActiveView = target;

        var closed = new List<string>();
        if (closeOthers)
        {
            foreach (var uiView in uiDoc.GetOpenUIViews())
            {
                if (uiView.ViewId == target.Id) continue;
                var name = document.GetElement(uiView.ViewId)?.Name;
                try
                {
                    uiView.Close();
                    closed.Add(name);
                }
                catch (Exception)
                {
                }
            }
        }

        if (target is View3D && JsonArgs.OptionalBool(args, "zoom", true))
        {
            uiDoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId == target.Id)?.ZoomToFit();
        }

        return new
        {
            document = document.Title,
            activeView = target.Name,
            viewType = target.ViewType.ToString(),
            closedViews = closed,
            openViews = uiDoc.GetOpenUIViews().Select(v => document.GetElement(v.ViewId)?.Name).ToList()
        };
    }

    private static bool Matches(Document doc, string title) =>
        string.Equals(doc.Title, title, StringComparison.OrdinalIgnoreCase)
        || string.Equals(System.IO.Path.GetFileNameWithoutExtension(doc.Title), title, StringComparison.OrdinalIgnoreCase);
}
