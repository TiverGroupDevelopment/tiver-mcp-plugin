using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Открывает семейство во вкладке редактора Revit (как «Редактировать семейство»): из закреплённой сессии,
/// из файла .rfa или из проекта по имени. Активирует вид, ставит изометрию и приближает к геометрии.
/// Фоновый документ сессии показать нельзя, поэтому сессия сохраняется, закрывается и файл открывается в окне;
/// дальше команды семейств работают с активным документом семейства.
/// </summary>
public class OpenFamilyInEditorEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Open family in editor";

    protected override object Run(UIApplication app, JObject args)
    {
        var path = JsonArgs.OptionalString(args, "path");
        var familyName = JsonArgs.OptionalString(args, "familyName");
        var save = JsonArgs.OptionalBool(args, "save", false);
        string note = null;
        string source;

        if (path != null)
        {
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) throw new ArgumentException($"Файл '{path}' не найден");
            source = "file";
        }
        else if (familyName != null && FamilySessionRegistry.Current == null)
        {
            path = CopyFromProject(app, familyName);
            source = "project";
            note = "Открыта копия семейства из проекта (временный файл). Изменения вернутся в проект через «Загрузить в проект» или load_family_into_project.";
        }
        else if (FamilySessionRegistry.Current != null)
        {
            path = ReleaseSession(FamilySessionRegistry.Current, save);
            source = "session";
            note = "Сессия закрыта, семейство открыто в окне Revit. Команды семейств работают с активным документом семейства.";
        }
        else if (app.ActiveUIDocument?.Document.IsFamilyDocument == true)
        {
            path = null;
            source = "active";
        }
        else
        {
            throw new ArgumentException("Нечего открывать: передайте path (.rfa) или familyName (семейство в проекте), либо откройте сессию (new_family / open_family)");
        }

        var uiDoc = path == null ? app.ActiveUIDocument : app.OpenAndActivateDocument(path);
        var doc = uiDoc.Document;
        var view = PickView(doc, JsonArgs.OptionalString(args, "view"));

        var isometric = JsonArgs.OptionalBool(args, "isometric", true);
        if (isometric && view is View3D view3D && !view3D.IsTemplate)
        {
            SetIsometric(doc, view3D);
        }

        if (!uiDoc.ActiveView.Id.Equals(view.Id)) uiDoc.ActiveView = view;
        if (JsonArgs.OptionalBool(args, "zoom", true)) ZoomToForms(uiDoc, view);

        return new
        {
            source,
            document = doc.Title,
            path = string.IsNullOrEmpty(doc.PathName) ? null : doc.PathName,
            category = doc.OwnerFamily?.FamilyCategory?.Name,
            view = FamilyDocInfo.DescribeView(view),
            note
        };
    }

    private static string CopyFromProject(UIApplication app, string familyName)
    {
        var project = ProjectResolver.Resolve(app, null);
        var family = FamilyEditSession.FindLoadedFamily(project, familyName);
        var directory = Path.Combine(Path.GetTempPath(), "revit-mcp", "editor");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, string.Concat(family.Name.Split(Path.GetInvalidFileNameChars())) + ".rfa");

        var familyDoc = project.EditFamily(family);
        try
        {
            FamilyFiles.SaveAsReplacing(familyDoc, target, true);
        }
        finally
        {
            familyDoc.Close(false);
        }

        return target;
    }

    private static string ReleaseSession(PinnedFamilySession session, bool save)
    {
        var doc = session.Document;
        var target = session.IsRelocated ? session.OriginalPath : (string.IsNullOrEmpty(doc.PathName) ? null : doc.PathName);
        if (target == null)
        {
            throw new InvalidOperationException("Семейство сессии ещё не сохранено в файл: вызовите save_family с path, затем open_family_in_editor");
        }

        if (doc.IsModified || session.IsRelocated)
        {
            if (!save)
            {
                throw new InvalidOperationException("В семействе сессии есть несохранённые изменения: передайте save: true (сохранить в " + target + ")");
            }

            if (session.IsRelocated) FamilyFiles.SaveAsReplacing(doc, target, true);
            else doc.Save();
        }

        doc.Close(false);
        session.DeleteCheckpointFiles();
        FamilySessionRegistry.Unpin();
        return target;
    }

    private static View PickView(Document doc, string name)
    {
        if (name != null) return FamilyDocInfo.FindView(doc, name);
        return FamilyDocInfo.Views(doc).FirstOrDefault(v => v is View3D && !v.IsTemplate)
               ?? FamilyDocInfo.Views(doc).FirstOrDefault()
               ?? throw new InvalidOperationException("В семействе нет видов");
    }

    private static void SetIsometric(Document doc, View3D view)
    {
        var forward = new XYZ(-1, 1, -1).Normalize();
        var up = forward.CrossProduct(XYZ.BasisZ).CrossProduct(forward).Normalize();
        if (up.Z < 0) up = up.Negate();
        var box = FormsBox(doc);
        var center = box == null ? XYZ.Zero : (box.Min + box.Max) / 2;

        var failures = new FailureSuppressor();
        using (var transaction = new Transaction(doc, "MCP: изометрия вида"))
        {
            failures.Attach(transaction);
            transaction.Start();
            view.SetOrientation(new ViewOrientation3D(center - forward * 100, up, forward));
            try
            {
                view.DisplayStyle = DisplayStyle.ShadingWithEdges;
                view.DetailLevel = ViewDetailLevel.Fine;
            }
            catch (Exception)
            {
            }

            transaction.Commit();
        }
    }

    private static void ZoomToForms(UIDocument uiDoc, View view)
    {
        var uiView = uiDoc.GetOpenUIViews().FirstOrDefault(v => v.ViewId.Equals(view.Id));
        if (uiView == null) return;
        uiView.ZoomToFit();
        var box = FormsBox(uiDoc.Document);
        if (box == null) return;
        var margin = (box.Max - box.Min) * 0.25;
        try
        {
            uiView.ZoomAndCenterRectangle(box.Min - margin, box.Max + margin);
        }
        catch (Exception)
        {
        }
    }

    private static BoundingBoxXYZ FormsBox(Document doc)
    {
        BoundingBoxXYZ result = null;
        foreach (var form in new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements().OfType<GenericForm>())
        {
            var b = form.get_BoundingBox(null);
            if (b == null) continue;
            if (result == null)
            {
                result = new BoundingBoxXYZ { Min = b.Min, Max = b.Max };
                continue;
            }

            result.Min = new XYZ(Math.Min(result.Min.X, b.Min.X), Math.Min(result.Min.Y, b.Min.Y), Math.Min(result.Min.Z, b.Min.Z));
            result.Max = new XYZ(Math.Max(result.Max.X, b.Max.X), Math.Max(result.Max.Y, b.Max.Y), Math.Max(result.Max.Z, b.Max.Z));
        }

        return result;
    }
}
