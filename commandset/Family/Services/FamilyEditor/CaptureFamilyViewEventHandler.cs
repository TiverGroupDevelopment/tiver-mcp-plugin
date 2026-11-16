using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Снимок вида семейства через Document.ExportImage: не зависит от окна Revit и работает,
/// когда оно перекрыто. Картинка возвращается как base64 PNG.
/// Без view строится временный изометрический 3D-вид, кадр подгоняется под геометрию.
/// На ортогональных видах опорные плоскости и уровни скрываются: иначе кадр растягивается на их длину
/// (метры), а деталь в несколько миллиметров превращается в точку. Все изменения откатываются.
/// </summary>
public class CaptureFamilyViewEventHandler : FamilyEditorEventHandlerBase
{
    private const int DefaultPixels = 1024;
    private const int MinPixels = 256;
    private const int MaxPixels = 4096;

    private static readonly BuiltInCategory[] Datums =
    {
        BuiltInCategory.OST_CLines,
        BuiltInCategory.OST_Levels,
        BuiltInCategory.OST_ReferenceLines,
        BuiltInCategory.OST_Dimensions
    };

    public override string GetName() => "Capture family view";

    protected override object Run(UIApplication app, JObject args)
    {
        var viewName = JsonArgs.OptionalString(args, "view");
        var pixels = (int)JsonArgs.OptionalDouble(args, "pixels", DefaultPixels);
        pixels = Math.Max(MinPixels, Math.Min(MaxPixels, pixels));
        var style = ParseStyle(JsonArgs.OptionalString(args, "style") ?? "shadedWithEdges");
        var hideDatums = JsonArgs.OptionalBool(args, "hideDatums", true);
        var hideConnectors = !JsonArgs.OptionalBool(args, "showConnectors", false);
        var direction = JsonArgs.Has(args, "direction") ? JsonArgs.Vector(args["direction"], "direction") : new XYZ(-1, 1, -1);
        if (direction.IsZeroLength()) throw new ArgumentException("direction не может быть нулевым");

        return Read(app, args, session =>
        {
            var doc = session.Document;
            var directory = Path.Combine(Path.GetTempPath(), "revit-mcp", "capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                object viewInfo;
                byte[] bytes;
                using (var group = new TransactionGroup(doc, "MCP: снимок вида"))
                {
                    group.Start();
                    try
                    {
                        var view = PrepareView(doc, viewName, style, hideDatums, hideConnectors, direction);
                        viewInfo = viewName == null
                            ? new { name = "(временный изометрический вид)", type = "ThreeD", viewDirection = FamilyDocInfo.Unit(direction.Normalize()) }
                            : FamilyDocInfo.DescribeView(view);
                        bytes = Export(doc, view, directory, pixels);
                    }
                    finally
                    {
                        // Временный вид и скрытие категорий не остаются в семействе.
                        group.RollBack();
                    }
                }

                return new
                {
                    view = viewInfo,
                    pixels,
                    style = style.ToString(),
                    mimeType = "image/png",
                    sizeBytes = bytes.Length,
                    imageBase64 = Convert.ToBase64String(bytes),
                    availableViews = FamilyDocInfo.Views(doc).Select(v => v.Name).ToList()
                };
            }
            finally
            {
                try
                {
                    Directory.Delete(directory, true);
                }
                catch (Exception)
                {
                }
            }
        });
    }

    private static View PrepareView(Document doc, string viewName, DisplayStyle style, bool hideDatums, bool hideConnectors, XYZ direction)
    {
        var failures = new FailureSuppressor();
        using (var transaction = new Transaction(doc, "MCP: подготовка вида"))
        {
            failures.Attach(transaction);
            transaction.Start();

            View view;
            if (viewName == null)
            {
                var type = new FilteredElementCollector(doc)
                               .OfClass(typeof(ViewFamilyType))
                               .Cast<ViewFamilyType>()
                               .FirstOrDefault(t => t.ViewFamily == ViewFamily.ThreeDimensional)
                           ?? throw new InvalidOperationException("В семействе нет типа 3D-вида");
                var view3D = View3D.CreateIsometric(doc, type.Id);
                view3D.SetOrientation(Orientation(doc, direction));
                view = view3D;
            }
            else
            {
                view = FamilyDocInfo.FindView(doc, viewName);
            }

            TrySet(() => view.DisplayStyle = style);
            TrySet(() => view.DetailLevel = ViewDetailLevel.Fine);
            // Значки соединителей рисуются в экранном размере и растягивают кадр так же, как опорные плоскости.
            var hidden = new List<BuiltInCategory>();
            if (hideDatums && !(view is View3D)) hidden.AddRange(Datums);
            if (hideConnectors) hidden.Add(BuiltInCategory.OST_ConnectorElem);
            foreach (var builtIn in hidden)
            {
                var category = Category.GetCategory(doc, builtIn);
                if (category != null && view.CanCategoryBeHidden(category.Id)) TrySet(() => view.SetCategoryHidden(category.Id, true));
            }

            if (transaction.Commit() != TransactionStatus.Committed)
            {
                throw new InvalidOperationException("Не удалось подготовить вид для снимка" + (failures.HasErrors ? ": " + string.Join("; ", failures.Errors) : string.Empty));
            }

            return view;
        }
    }

    /// <summary>Взгляд в направлении direction на центр геометрии; «вверх» — ближе всего к +Z.</summary>
    private static ViewOrientation3D Orientation(Document doc, XYZ direction)
    {
        var forward = direction.Normalize();
        var up = forward.CrossProduct(XYZ.BasisZ).CrossProduct(forward);
        if (up.IsZeroLength()) up = XYZ.BasisY;
        up = up.Normalize();
        if (up.Z < 0) up = up.Negate();

        var center = XYZ.Zero;
        // OfClass(GenericForm) Revit не принимает (базовый класс), поэтому фильтруем после выборки.
        var forms = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements().OfType<GenericForm>();
        BoundingBoxXYZ box = null;
        foreach (var form in forms)
        {
            var b = form.get_BoundingBox(null);
            if (b == null) continue;
            if (box == null)
            {
                box = new BoundingBoxXYZ { Min = b.Min, Max = b.Max };
            }
            else
            {
                box.Min = new XYZ(Math.Min(box.Min.X, b.Min.X), Math.Min(box.Min.Y, b.Min.Y), Math.Min(box.Min.Z, b.Min.Z));
                box.Max = new XYZ(Math.Max(box.Max.X, b.Max.X), Math.Max(box.Max.Y, b.Max.Y), Math.Max(box.Max.Z, b.Max.Z));
            }
        }

        if (box != null) center = (box.Min + box.Max) / 2;
        return new ViewOrientation3D(center - forward * 100, up, forward);
    }

    private static byte[] Export(Document doc, View view, string directory, int pixels)
    {
        var options = new ImageExportOptions
        {
            FilePath = Path.Combine(directory, "view"),
            ExportRange = ExportRange.SetOfViews,
            ZoomType = ZoomFitType.FitToPage,
            FitDirection = FitDirectionType.Horizontal,
            PixelSize = pixels,
            ImageResolution = ImageResolution.DPI_150,
            HLRandWFViewsFileType = ImageFileType.PNG,
            ShadowViewsFileType = ImageFileType.PNG
        };
        options.SetViewsAndSheets(new List<ElementId> { view.Id });
        doc.ExportImage(options);

        // Revit дописывает к имени файла тип и название вида, поэтому берём то, что появилось в пустой папке.
        var file = new DirectoryInfo(directory).GetFiles("*.png").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()
                   ?? throw new InvalidOperationException($"Revit не создал изображение вида '{view.Name}'");
        return File.ReadAllBytes(file.FullName);
    }

    private static DisplayStyle ParseStyle(string value)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "wireframe": return DisplayStyle.Wireframe;
            case "hiddenline": return DisplayStyle.HLR;
            case "shaded": return DisplayStyle.Shading;
            case "shadedwithedges": return DisplayStyle.ShadingWithEdges;
            case "realistic": return DisplayStyle.Realistic;
            default:
                throw new ArgumentException($"style: допустимо wireframe, hiddenLine, shaded, shadedWithEdges, realistic; получено '{value}'");
        }
    }

    private static void TrySet(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
        }
    }
}
