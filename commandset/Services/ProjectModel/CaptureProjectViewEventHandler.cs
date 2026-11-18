using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.ProjectModel;

namespace RevitMCPCommandSet.Services.ProjectModel;

/// <summary>
/// 3D-вид проекта с рамкой 3D (по координатам или по элементам), направлением, стилем и скрытием;
/// экспортирует PNG и возвращает его картинкой. Вид сохраняется в проекте и может быть открыт в Revit.
/// </summary>
public class CaptureProjectViewEventHandler : ProjectModelEventHandlerBase
{
    public override string GetName() => "Capture project view";

    protected override object Run(UIApplication app, JObject args)
    {
        var doc = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var name = JsonArgs.OptionalString(args, "name") ?? "MCP_3D";
        var pixels = (int)JsonArgs.OptionalDouble(args, "pixels", 1400);
        var style = (JsonArgs.OptionalString(args, "style") ?? "shaded").ToLowerInvariant();
        var direction = JsonArgs.Has(args, "direction") ? JsonArgs.Vector(args["direction"], "direction").Normalize() : new XYZ(-1, 1, -1).Normalize();
        var margin = JsonArgs.OptionalDouble(args, "marginMm", 100) / ProjectModelUtils.MmPerFoot;
        var hideTypes = (args["hideTypes"] as JArray)?.Select(t => t.Value<string>()).ToList() ?? new List<string>();
        var open = JsonArgs.OptionalBool(args, "open", true);

        BoundingBoxXYZ box = null;
        if (args["sectionBox"] is JObject sb)
        {
            box = new BoundingBoxXYZ { Min = JsonArgs.PointMm(sb["min"], "sectionBox.min"), Max = JsonArgs.PointMm(sb["max"], "sectionBox.max") };
        }
        else if (JsonArgs.Has(args, "elementIds"))
        {
            XYZ min = null, max = null;
            foreach (var id in JsonArgs.Ids(args, "elementIds"))
            {
                var bb = doc.GetElement(FamilyEditorIds.FromNumber(id))?.get_BoundingBox(null);
                if (bb == null) continue;
                min = min == null ? bb.Min : new XYZ(Math.Min(min.X, bb.Min.X), Math.Min(min.Y, bb.Min.Y), Math.Min(min.Z, bb.Min.Z));
                max = max == null ? bb.Max : new XYZ(Math.Max(max.X, bb.Max.X), Math.Max(max.Y, bb.Max.Y), Math.Max(max.Z, bb.Max.Z));
            }

            if (min == null) throw new ArgumentException("У elementIds нет геометрии");
            box = new BoundingBoxXYZ { Min = min - new XYZ(margin, margin, margin), Max = max + new XYZ(margin, margin, margin) };
        }

        var view = InTransaction(doc, $"MCP: вид {name}", failures =>
        {
            var v = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().FirstOrDefault(x => !x.IsTemplate && x.Name == name);
            if (v == null)
            {
                var type = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().First(t => t.ViewFamily == ViewFamily.ThreeDimensional);
                v = View3D.CreateIsometric(doc, type.Id);
                v.Name = name;
            }

            v.DetailLevel = ViewDetailLevel.Fine;
            v.DisplayStyle = style == "shadedwithedges" ? DisplayStyle.ShadingWithEdges
                : style == "hiddenline" ? DisplayStyle.HLR
                : style == "realistic" ? DisplayStyle.Realistic
                : style == "wireframe" ? DisplayStyle.Wireframe
                : DisplayStyle.Shading;

            var right = direction.CrossProduct(XYZ.BasisZ);
            if (right.GetLength() < 1e-6) right = XYZ.BasisX;
            var up = right.Normalize().CrossProduct(direction).Normalize();
            var center = box != null ? (box.Min + box.Max) / 2 : XYZ.Zero;
            v.SetOrientation(new ViewOrientation3D(center - direction * 100, up, direction));

            foreach (var bic in new[] { BuiltInCategory.OST_Levels, BuiltInCategory.OST_Grids, BuiltInCategory.OST_SectionBox, BuiltInCategory.OST_Cameras })
            {
                var c = Category.GetCategory(doc, bic);
                if (c != null && v.CanCategoryBeHidden(c.Id)) v.SetCategoryHidden(c.Id, true);
            }

            if (box != null)
            {
                v.IsSectionBoxActive = true;
                v.SetSectionBox(box);
            }

            doc.Regenerate();
            if (hideTypes.Count > 0)
            {
                var toHide = new FilteredElementCollector(doc, v.Id).WhereElementIsNotElementType()
                    .Where(e => doc.GetElement(e.GetTypeId()) is ElementType t && hideTypes.Any(h => ProjectModelUtils.NameMatches(t, h)))
                    .Select(e => e.Id).ToList();
                if (toHide.Count > 0) v.HideElements(toHide);
            }

            if (box != null) FitCrop(v, box);
            return v;
        });

        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "revit-mcp", "views");
        System.IO.Directory.CreateDirectory(folder);
        var stem = System.IO.Path.Combine(folder, Guid.NewGuid().ToString("N"));
        var options = new ImageExportOptions
        {
            ExportRange = ExportRange.SetOfViews,
            FilePath = stem,
            ZoomType = ZoomFitType.FitToPage,
            PixelSize = pixels,
            ImageResolution = ImageResolution.DPI_150,
            HLRandWFViewsFileType = ImageFileType.PNG,
            ShadowViewsFileType = ImageFileType.PNG
        };
        options.SetViewsAndSheets(new List<ElementId> { view.Id });
        doc.ExportImage(options);
        var file = System.IO.Directory.GetFiles(folder, System.IO.Path.GetFileName(stem) + "*.png").FirstOrDefault()
                   ?? throw new InvalidOperationException("Revit не создал изображение");
        var bytes = System.IO.File.ReadAllBytes(file);

        var opened = false;
        if (open && app.ActiveUIDocument != null && app.ActiveUIDocument.Document.Equals(doc))
        {
            app.ActiveUIDocument.RequestViewChange(view);
            opened = true;
        }

        return new
        {
            project = doc.Title,
            view = view.Name,
            viewId = view.Id.GetValue(),
            openedInRevit = opened,
            file,
            mimeType = "image/png",
            imageBase64 = Convert.ToBase64String(bytes)
        };
    }

    /// <summary>Рамка подрезки вида по проекции рамки 3D: экспорт вписывает в кадр только её.</summary>
    private static void FitCrop(View3D view, BoundingBoxXYZ box)
    {
        var crop = view.CropBox;
        var inverse = crop.Transform.Inverse;
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var x in new[] { box.Min.X, box.Max.X })
        foreach (var y in new[] { box.Min.Y, box.Max.Y })
        foreach (var z in new[] { box.Min.Z, box.Max.Z })
        {
            var p = inverse.OfPoint(new XYZ(x, y, z));
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }

        var pad = Math.Max(maxX - minX, maxY - minY) * 0.03;
        crop.Min = new XYZ(minX - pad, minY - pad, crop.Min.Z);
        crop.Max = new XYZ(maxX + pad, maxY + pad, crop.Max.Z);
        view.CropBox = crop;
        view.CropBoxActive = true;
        view.CropBoxVisible = false;
    }
}
