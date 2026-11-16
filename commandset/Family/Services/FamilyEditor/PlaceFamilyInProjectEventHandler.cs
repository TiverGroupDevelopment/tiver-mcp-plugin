using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Размещает экземпляр семейства в проекте: по имени семейства и типа, на уровне по имени, в точке (мм)
/// с поворотом. Семейство можно сначала загрузить из .rfa (path). Сбои Revit и сторонних updater
/// возвращаются текстом, транзакция откатывается без окон.
/// </summary>
public class PlaceFamilyInProjectEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Place family in project";

    protected override object Run(UIApplication app, JObject args)
    {
        var project = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var path = JsonArgs.OptionalString(args, "path");
        var familyName = JsonArgs.OptionalString(args, "family");
        if (path == null && familyName == null) throw new ArgumentException("Укажите family (имя семейства в проекте) или path (.rfa для загрузки)");

        string loadedFrom = null;
        if (path != null)
        {
            familyName = LoadFromFile(project, path, JsonArgs.OptionalBool(args, "overwriteProjectValues", true));
            loadedFrom = path;
        }

        var family = new FilteredElementCollector(project)
                         .OfClass(typeof(Family))
                         .Cast<Family>()
                         .FirstOrDefault(f => string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase))
                     ?? throw new ArgumentException($"Семейство '{familyName}' не загружено в проект '{project.Title}' (list_project_families)");

        var symbols = family.GetFamilySymbolIds().Select(id => (FamilySymbol)project.GetElement(id)).ToList();
        var typeName = JsonArgs.OptionalString(args, "type");
        var symbol = typeName == null
            ? symbols.FirstOrDefault()
            : symbols.FirstOrDefault(s => string.Equals(s.Name, typeName, StringComparison.OrdinalIgnoreCase));
        if (symbol == null)
        {
            throw new ArgumentException($"Тип '{typeName}' не найден в семействе '{family.Name}'. Типы: {string.Join(", ", symbols.Select(s => s.Name))}");
        }

        var placement = family.FamilyPlacementType;
        if (placement == FamilyPlacementType.OneLevelBasedHosted
            || placement == FamilyPlacementType.CurveBased
            || placement == FamilyPlacementType.CurveBasedDetail
            || placement == FamilyPlacementType.CurveDrivenStructural
            || placement == FamilyPlacementType.ViewBased)
        {
            throw new InvalidOperationException(
                $"Семейство '{family.Name}' ({family.FamilyPlacementType}) требует основу или линию; эта команда ставит только свободно размещаемые семейства и семейства по уровню");
        }

        var level = ResolveLevel(app, project, JsonArgs.OptionalString(args, "level"));
        var location = JsonArgs.Has(args, "location") ? JsonArgs.PointMm(args["location"], "location") : XYZ.Zero;
        var rotation = JsonArgs.OptionalDouble(args, "rotation", 0);

        var failures = new FailureSuppressor();
        FamilyInstance instance;
        using (var transaction = new Transaction(project, $"MCP: разместить {family.Name}"))
        {
            failures.Attach(transaction);
            transaction.Start();
            if (!symbol.IsActive) symbol.Activate();
            instance = project.Create.NewFamilyInstance(new XYZ(location.X, location.Y, level.Elevation), symbol, level, StructuralType.NonStructural);
            if (Math.Abs(location.Z) > 1e-9) SetOffset(instance, location.Z);
            if (Math.Abs(rotation) > 1e-9)
            {
                var point = new XYZ(location.X, location.Y, level.Elevation);
                ElementTransformUtils.RotateElement(project, instance.Id, Line.CreateBound(point, point + XYZ.BasisZ), rotation * Math.PI / 180);
            }

            var status = transaction.Commit();
            if (status != TransactionStatus.Committed || failures.HasErrors)
            {
                throw new InvalidOperationException(
                    $"Revit отменил размещение ({status}): {string.Join("; ", failures.Errors.DefaultIfEmpty("без сообщения"))}. Если ошибка от стороннего updater — см. diagnose_revit.");
            }
        }

        var shown = false;
        var uiDoc = app.ActiveUIDocument;
        if (uiDoc != null && uiDoc.Document.Equals(project))
        {
            if (JsonArgs.OptionalBool(args, "select", true)) uiDoc.Selection.SetElementIds(new List<ElementId> { instance.Id });
            if (JsonArgs.OptionalBool(args, "show", true))
            {
                try
                {
                    uiDoc.ShowElements(instance.Id);
                    shown = true;
                }
                catch (Exception)
                {
                }
            }
        }

        var box = instance.get_BoundingBox(null);
        return new
        {
            id = instance.Id.GetValue(),
            project = project.Title,
            family = family.Name,
            type = symbol.Name,
            category = instance.Category?.Name,
            level = level.Name,
            loadedFrom,
            boundingBoxMm = box == null ? null : new { min = FamilyDocInfo.Mm(box.Min), max = FamilyDocInfo.Mm(box.Max) },
            connectors = instance.MEPModel?.ConnectorManager?.Connectors?.Size ?? 0,
            shownInView = shown,
            warnings = failures.Warnings
        };
    }

    private static string LoadFromFile(Document project, string path, bool overwriteValues)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        if (!System.IO.File.Exists(fullPath)) throw new ArgumentException($"Файл '{fullPath}' не найден");

        var failures = new FailureSuppressor();
        using (var transaction = new Transaction(project, "MCP: загрузить семейство"))
        {
            failures.Attach(transaction);
            transaction.Start();
            project.LoadFamily(fullPath, new FamilyLoadOptions(overwriteValues), out var family);
            if (transaction.Commit() != TransactionStatus.Committed || failures.HasErrors)
            {
                throw new InvalidOperationException($"Семейство из '{fullPath}' не загружено: {string.Join("; ", failures.Errors)}");
            }

            // family == null, если такое семейство уже загружено и не изменилось.
            return family?.Name ?? System.IO.Path.GetFileNameWithoutExtension(fullPath);
        }
    }

    private static Level ResolveLevel(UIApplication app, Document project, string name)
    {
        var levels = new FilteredElementCollector(project).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
        if (levels.Count == 0) throw new InvalidOperationException($"В проекте '{project.Title}' нет уровней");
        if (name != null)
        {
            return levels.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
                   ?? throw new ArgumentException($"Уровень '{name}' не найден. Уровни: {string.Join(", ", levels.Select(l => l.Name))}");
        }

        // Без имени — уровень активного плана, иначе ближайший к нулю.
        var view = app.ActiveUIDocument?.Document.Equals(project) == true ? app.ActiveUIDocument.ActiveView : null;
        return view?.GenLevel ?? levels.OrderBy(l => Math.Abs(l.Elevation)).First();
    }

    private static void SetOffset(FamilyInstance instance, double offsetFeet)
    {
        foreach (var builtIn in new[] { BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM, BuiltInParameter.INSTANCE_ELEVATION_PARAM })
        {
            var parameter = instance.get_Parameter(builtIn);
            if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.Double)
            {
                parameter.Set(offsetFeet);
                return;
            }
        }
    }
}

/// <summary>Выбор проекта: по имени, иначе активный, иначе единственный открытый.</summary>
public static class ProjectResolver
{
    public static Document Resolve(UIApplication app, string title)
    {
        var projects = app.Application.Documents.Cast<Document>().Where(d => !d.IsFamilyDocument && !d.IsLinked).ToList();
        if (title != null)
        {
            return projects.FirstOrDefault(d => string.Equals(d.Title, title, StringComparison.OrdinalIgnoreCase)
                                                || string.Equals(System.IO.Path.GetFileNameWithoutExtension(d.Title), title, StringComparison.OrdinalIgnoreCase))
                   ?? throw new ArgumentException($"Проект '{title}' не открыт. Открытые проекты: {string.Join(", ", projects.Select(d => d.Title))}");
        }

        var active = app.ActiveUIDocument?.Document;
        if (active != null && !active.IsFamilyDocument) return active;
        if (projects.Count == 1) return projects[0];
        throw new InvalidOperationException(projects.Count == 0
            ? "В Revit не открыт ни один проект"
            : $"Открыто несколько проектов, укажите project: {string.Join(", ", projects.Select(d => d.Title))}");
    }
}
