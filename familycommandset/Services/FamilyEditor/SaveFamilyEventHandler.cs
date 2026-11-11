using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

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

            if (path == null)
            {
                if (session.IsDetached || string.IsNullOrEmpty(doc.PathName))
                {
                    throw new ArgumentException("Документ ещё не сохранён на диск: укажите path (*.rfa)");
                }

                doc.Save();
                return new { path = doc.PathName, mode = "save" };
            }

            if (!path.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("path должен оканчиваться на .rfa");
            }

            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                throw new ArgumentException($"Папка '{directory}' не существует");
            }

            var existed = File.Exists(fullPath);
            if (existed && !overwrite)
            {
                throw new InvalidOperationException($"Файл '{fullPath}' уже существует. Для замены передайте overwrite: true");
            }

            doc.SaveAs(fullPath, new SaveAsOptions { OverwriteExistingFile = overwrite });
            return new { path = fullPath, mode = "saveAs", overwritten = existed };
        });
    }
}
