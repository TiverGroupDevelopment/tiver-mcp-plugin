using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class SaveFamilyEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Save family";

    protected override object Run(UIApplication app, JObject args)
    {
        var path = JsonArgs.OptionalString(args, "path");
        var overwrite = JsonArgs.OptionalBool(args, "overwrite", false);

        return Read(app, args, session =>
        {
            var doc = session.Document;
            var pinned = session.IsPinned ? FamilySessionRegistry.Current : null;

            if (path == null)
            {
                // После контрольной точки документ лежит во временной папке: сохраняем в его настоящий файл.
                if (pinned != null && pinned.IsRelocated)
                {
                    var original = pinned.OriginalPath
                                   ?? throw new ArgumentException("Семейство ещё не сохранялось в свой файл: укажите path (*.rfa)");
                    doc.SaveAs(original, new SaveAsOptions { OverwriteExistingFile = true });
                    return new { path = original, mode = "save" };
                }

                if (session.IsDetached || string.IsNullOrEmpty(doc.PathName))
                {
                    throw new ArgumentException("Документ ещё не сохранён на диск: укажите path (*.rfa)");
                }

                doc.Save();
                return new { path = doc.PathName, mode = "save" };
            }

            var fullPath = FamilyFiles.ValidateSavePath(path, overwrite, out var existed);
            doc.SaveAs(fullPath, new SaveAsOptions { OverwriteExistingFile = overwrite });
            if (pinned != null) pinned.OriginalPath = fullPath;
            return new { path = fullPath, mode = "saveAs", overwritten = existed };
        });
    }
}
