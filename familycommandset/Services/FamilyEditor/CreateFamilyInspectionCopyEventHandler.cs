using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Копия семейства для инспектора: рядом с исходником сохраняется «&lt;имя&gt;_инспекция.rfa», в ней по крайним точкам
/// геометрии строятся опорные плоскости «ИНСП_…» и габаритные размеры на плане и фасадах, масштаб видов подбирается
/// под размер детали. Исходное семейство не меняется.
/// </summary>
public class CreateFamilyInspectionCopyEventHandler : FamilyEditorEventHandlerBase
{
    private static readonly int[] Scales = { 1, 2, 5, 10, 20, 50, 100 };

    public override string GetName() => "Create family inspection copy";

    protected override object Run(UIApplication app, JObject args)
    {
        var path = JsonArgs.OptionalString(args, "path");
        var target = JsonArgs.OptionalString(args, "targetPath");
        var overwrite = JsonArgs.OptionalBool(args, "overwrite", true);
        var open = JsonArgs.OptionalBool(args, "open", true);
        var offsetMm = JsonArgs.OptionalDouble(args, "offsetMm", 0);
        string note = null;
        string source;

        if (path != null)
        {
            source = Path.GetFullPath(path);
            if (!File.Exists(source)) throw new ArgumentException($"Файл '{source}' не найден");
            target = Target(source, target, overwrite);
            File.Copy(source, target, overwrite);
        }
        else
        {
            string copied = null;
            Read(app, args, session =>
            {
                var doc = session.Document;
                var pinned = session.IsPinned ? FamilySessionRegistry.Current : null;
                var file = pinned != null && pinned.IsRelocated ? pinned.OriginalPath : doc.PathName;
                if (!session.IsPinned && string.IsNullOrEmpty(doc.PathName) && !doc.Equals(app.ActiveUIDocument?.Document))
                {
                    // Временная копия из проекта (EditFamily): сохраняем её как файл инспекции.
                    copied = Target(Path.Combine(Path.GetTempPath(), "revit-mcp", session.FamilyName + ".rfa"), target, overwrite);
                    Directory.CreateDirectory(Path.GetDirectoryName(copied));
                    FamilyFiles.SaveAsReplacing(doc, copied, overwrite);
                    return null;
                }

                if (string.IsNullOrEmpty(file)) throw new InvalidOperationException("Семейство ещё не сохранено в файл: сохраните его (save_family с path) или передайте path");
                if (doc.IsModified || (pinned?.IsRelocated ?? false))
                {
                    note = "В семействе есть несохранённые изменения: в копию попало последнее сохранённое состояние. Сохраните семейство и повторите, если нужно.";
                }

                copied = Target(file, target, overwrite);
                File.Copy(file, copied, overwrite);
                return null;
            });
            source = "current";
            target = copied;
        }

        var result = Annotate(app, target, offsetMm);

        if (open)
        {
            var uiDoc = app.OpenAndActivateDocument(target);
            var front = FamilyDocInfo.Views(uiDoc.Document).FirstOrDefault(v => v.ViewType == ViewType.Elevation && v.ViewDirection.IsAlmostEqualTo(new XYZ(0, -1, 0)));
            if (front != null) uiDoc.ActiveView = front;
        }

        return new { source, copy = target, opened = open, note, result };
    }

    private static string Target(string source, string target, bool overwrite)
    {
        var path = target != null
            ? Path.GetFullPath(target)
            : Path.Combine(Path.GetDirectoryName(source) ?? string.Empty, Path.GetFileNameWithoutExtension(source) + "_инспекция.rfa");
        if (!path.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("targetPath должен оканчиваться на .rfa");
        if (File.Exists(path) && !overwrite) throw new InvalidOperationException($"Файл '{path}' уже существует. Передайте overwrite: true");
        return path;
    }

    private static object Annotate(UIApplication app, string path, double offsetMm)
    {
        var doc = app.Application.OpenDocumentFile(path);
        try
        {
            var box = GeometryBox(doc) ?? throw new InvalidOperationException("В семействе нет геометрии для обмера");
            var size = box.Max - box.Min;
            var maxMm = Math.Max(size.X, Math.Max(size.Y, size.Z)) * 304.8;
            var scale = Scales.FirstOrDefault(s => maxMm / s <= 120);
            if (scale == 0) scale = Scales[Scales.Length - 1];
            var offset = (offsetMm > 0 ? offsetMm : Math.Max(maxMm * 0.15, 3)) / 304.8;

            var views = FamilyDocInfo.Views(doc);
            var plan = views.FirstOrDefault(v => v.ViewType == ViewType.FloorPlan);
            var front = views.FirstOrDefault(v => v.ViewType == ViewType.Elevation && v.ViewDirection.IsAlmostEqualTo(new XYZ(0, -1, 0)));
            var side = views.FirstOrDefault(v => v.ViewType == ViewType.Elevation && Math.Abs(v.ViewDirection.X) > 0.99);
            var created = new List<object>();
            var failures = new FailureSuppressor();

            using (var transaction = new Transaction(doc, "MCP: размеры для инспекции"))
            {
                failures.Attach(transaction);
                transaction.Start();
                foreach (var view in new[] { plan, front, side }.Where(v => v != null))
                {
                    try
                    {
                        view.Scale = scale;
                    }
                    catch (Exception)
                    {
                    }
                }

                var extent = Math.Max(maxMm / 304.8, 0.1);
                var xMin = Plane(doc, plan, "ИНСП_X_мин", new XYZ(box.Min.X, 0, 0), XYZ.BasisX, extent);
                var xMax = Plane(doc, plan, "ИНСП_X_макс", new XYZ(box.Max.X, 0, 0), XYZ.BasisX, extent);
                var yMin = Plane(doc, plan, "ИНСП_Y_мин", new XYZ(0, box.Min.Y, 0), XYZ.BasisY, extent);
                var yMax = Plane(doc, plan, "ИНСП_Y_макс", new XYZ(0, box.Max.Y, 0), XYZ.BasisY, extent);
                var zMin = Plane(doc, front, "ИНСП_Z_мин", new XYZ(0, 0, box.Min.Z), XYZ.BasisZ, extent);
                var zMax = Plane(doc, front, "ИНСП_Z_макс", new XYZ(0, 0, box.Max.Z), XYZ.BasisZ, extent);
                doc.Regenerate();

                created.Add(Dimension(doc, plan, xMin, xMax, XYZ.BasisX, new XYZ(0, box.Max.Y + offset, 0), "X (длина) на плане"));
                created.Add(Dimension(doc, plan, yMin, yMax, XYZ.BasisY, new XYZ(box.Max.X + offset, 0, 0), "Y (ширина) на плане"));
                created.Add(Dimension(doc, front, xMin, xMax, XYZ.BasisX, new XYZ(0, 0, box.Max.Z + offset), "X (длина) на фасаде"));
                created.Add(Dimension(doc, front, zMin, zMax, XYZ.BasisZ, new XYZ(box.Max.X + offset, 0, 0), "Z (высота) на фасаде"));
                if (side != null)
                {
                    created.Add(Dimension(doc, side, yMin, yMax, XYZ.BasisY, new XYZ(0, 0, box.Max.Z + offset), "Y (ширина) на боковом фасаде"));
                }

                if (transaction.Commit() != TransactionStatus.Committed)
                {
                    throw new InvalidOperationException("Revit отменил обмер: " + string.Join("; ", failures.Errors));
                }
            }

            doc.Save();
            return new
            {
                overallMm = new[] { Math.Round(size.X * 304.8, 2), Math.Round(size.Y * 304.8, 2), Math.Round(size.Z * 304.8, 2) },
                minMm = FamilyDocInfo.Mm(box.Min),
                maxMm = FamilyDocInfo.Mm(box.Max),
                viewScale = "1:" + scale,
                dimensions = created,
                connectors = new FilteredElementCollector(doc).OfClass(typeof(ConnectorElement)).Cast<ConnectorElement>()
                    .Select(c => new
                    {
                        id = c.Id.GetValue(),
                        domain = c.Domain.ToString(),
                        originMm = FamilyDocInfo.Mm(c.CoordinateSystem.Origin),
                        directionZ = FamilyDocInfo.Unit(c.CoordinateSystem.BasisZ),
                        diameter = c.get_Parameter(BuiltInParameter.CONNECTOR_DIAMETER)?.AsValueString()
                    })
                    .ToList(),
                warnings = failures.Warnings
            };
        }
        finally
        {
            doc.Close(false);
        }
    }

    private static ReferencePlane Plane(Document doc, View view, string name, XYZ point, XYZ normal, double extent)
    {
        if (view == null) return null;
        // Линия плоскости лежит в плоскости вида, cutVec — направление взгляда вида.
        var cut = view.ViewDirection.Normalize();
        var along = normal.CrossProduct(cut).Normalize();
        var plane = doc.FamilyCreate.NewReferencePlane(point - along * extent, point + along * extent, cut, view);
        plane.Name = name;
        return plane;
    }

    private static object Dimension(Document doc, View view, ReferencePlane a, ReferencePlane b, XYZ direction, XYZ anchor, string what)
    {
        if (view == null || a == null || b == null) return new { what, created = false, reason = "нет вида" };
        try
        {
            var start = a.GetPlane().Origin.DotProduct(direction);
            var end = b.GetPlane().Origin.DotProduct(direction);
            var inView = anchor - view.ViewDirection * (anchor - view.Origin).DotProduct(view.ViewDirection);
            var basePoint = inView - direction * inView.DotProduct(direction);
            var line = Line.CreateBound(basePoint + direction * start, basePoint + direction * end);
            var references = new ReferenceArray();
            references.Append(a.GetReference());
            references.Append(b.GetReference());
            var dimension = doc.FamilyCreate.NewLinearDimension(view, line, references);
            return new { what, created = dimension != null, valueMm = Math.Round(Math.Abs(end - start) * 304.8, 2), view = view.Name };
        }
        catch (Exception ex)
        {
            return new { what, created = false, reason = ex.Message };
        }
    }

    /// <summary>Габариты твёрдых форм и вложенных экземпляров (без пустотных форм и аннотаций).</summary>
    private static BoundingBoxXYZ GeometryBox(Document doc)
    {
        BoundingBoxXYZ result = null;
        var elements = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements()
            .Where(e => (e is GenericForm form && form.IsSolid) || e is FamilyInstance);
        foreach (var element in elements)
        {
            var b = element.get_BoundingBox(null);
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
