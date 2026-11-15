using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>Семейства, загруженные в проект: категория, размещение, типы и число экземпляров.</summary>
public class ListProjectFamiliesEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "List project families";

    protected override object Run(UIApplication app, JObject args)
    {
        var project = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var nameFilter = JsonArgs.OptionalString(args, "nameFilter");
        var categoryFilter = JsonArgs.OptionalString(args, "category");
        var includeSystem = JsonArgs.OptionalBool(args, "includeNonEditable", false);

        var instanceCounts = new FilteredElementCollector(project)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .GroupBy(i => i.Symbol?.Family?.Id.GetValue() ?? -1)
            .ToDictionary(g => g.Key, g => g.Count());

        var families = new FilteredElementCollector(project)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => includeSystem || f.IsEditable)
            .Where(f => nameFilter == null || f.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0)
            .Where(f => categoryFilter == null
                        || string.Equals(f.FamilyCategory?.Name, categoryFilter, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(FamilyProperties.BuiltInCategoryName(f.FamilyCategory), categoryFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.FamilyCategory?.Name)
            .ThenBy(f => f.Name)
            .Select(f => new
            {
                id = f.Id.GetValue(),
                name = f.Name,
                category = f.FamilyCategory?.Name,
                builtInCategory = FamilyProperties.BuiltInCategoryName(f.FamilyCategory),
                placementType = f.FamilyPlacementType.ToString(),
                isEditable = f.IsEditable,
                isInPlace = f.IsInPlace,
                instances = instanceCounts.TryGetValue(f.Id.GetValue(), out var count) ? count : 0,
                types = f.GetFamilySymbolIds().Select(id => project.GetElement(id)).Where(e => e != null)
                    .Select(e => new { id = e.Id.GetValue(), name = e.Name }).ToList()
            })
            .ToList();

        return new { project = project.Title, count = families.Count, families };
    }
}
