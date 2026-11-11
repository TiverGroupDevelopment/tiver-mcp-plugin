using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class LoadNestedFamilyEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Load nested family";

    protected override object Run(UIApplication app, JObject args)
    {
        var path = JsonArgs.RequiredString(args, "path");
        if (!path.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("path должен оканчиваться на .rfa");
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new ArgumentException($"Файл '{fullPath}' не найден");
        var overwriteValues = JsonArgs.OptionalBool(args, "overwriteParameterValues", true);

        return Edit(app, args, "MCP: load nested family", doc =>
        {
            if (string.Equals(doc.PathName, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Семейство нельзя вложить само в себя");
            }

            var loaded = doc.LoadFamily(fullPath, new FamilyLoadOptions(overwriteValues), out var family);
            if (family == null)
            {
                // Revit возвращает false и null, когда такое же семейство уже загружено без изменений.
                var name = Path.GetFileNameWithoutExtension(fullPath);
                family = new FilteredElementCollector(doc)
                             .OfClass(typeof(Family))
                             .Cast<Family>()
                             .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                         ?? throw new InvalidOperationException($"Revit не загрузил семейство '{fullPath}'");
            }

            return new
            {
                loaded,
                family = family.Name,
                familyId = family.Id.GetValue(),
                category = family.FamilyCategory?.Name,
                placementType = family.FamilyPlacementType.ToString(),
                types = family.GetFamilySymbolIds()
                    .Select(id => doc.GetElement(id))
                    .Where(e => e != null)
                    .Select(e => new { id = e.Id.GetValue(), name = e.Name })
                    .ToList()
            };
        });
    }
}
