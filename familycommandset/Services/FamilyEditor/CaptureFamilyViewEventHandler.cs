using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Снимок вида семейства через Document.ExportImage: не зависит от окна Revit и работает,
/// когда оно перекрыто. Картинка возвращается как base64 PNG.
/// </summary>
public class CaptureFamilyViewEventHandler : FamilyEditorEventHandlerBase
{
    private const int DefaultPixels = 1024;
    private const int MinPixels = 256;
    private const int MaxPixels = 4096;

    public override string GetName() => "Capture family view";

    protected override object Run(UIApplication app, JObject args)
    {
        var viewName = JsonArgs.OptionalString(args, "view");
        var pixels = (int)JsonArgs.OptionalDouble(args, "pixels", DefaultPixels);
        pixels = Math.Max(MinPixels, Math.Min(MaxPixels, pixels));

        return Read(app, args, session =>
        {
            var doc = session.Document;
            var view = viewName != null
                ? FamilyDocInfo.FindView(doc, viewName)
                : FamilyDocInfo.Views(doc).FirstOrDefault(v => v.ViewType == ViewType.ThreeD)
                  ?? FamilyDocInfo.Views(doc).FirstOrDefault()
                  ?? throw new InvalidOperationException("В семействе нет видов для снимка");

            var directory = Path.Combine(Path.GetTempPath(), "revit-mcp", "capture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
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
                var bytes = File.ReadAllBytes(file.FullName);

                return new
                {
                    view = FamilyDocInfo.DescribeView(view),
                    pixels,
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
}
