using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class CreateFamilySubcategoriesEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family subcategories";

    protected override object Run(UIApplication app, JObject args)
    {
        var specs = JsonArgs.RequiredArray(args, "subcategories").OfType<JObject>().ToList();
        if (specs.Count == 0) throw new ArgumentException("subcategories пуст");

        return Edit(app, args, "MCP: create family subcategories", doc =>
        {
            var parent = doc.OwnerFamily?.FamilyCategory
                         ?? throw new InvalidOperationException("У семейства не задана категория");
            var result = new List<object>();

            foreach (var spec in specs)
            {
                var name = JsonArgs.RequiredString(spec, "name");
                Category subcategory = null;
                foreach (Category existing in parent.SubCategories)
                {
                    if (string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)) subcategory = existing;
                }

                var created = subcategory == null;
                subcategory ??= doc.Settings.Categories.NewSubcategory(parent, name);

                if (JsonArgs.Has(spec, "color"))
                {
                    subcategory.LineColor = FamilyMaterials.ParseColor(spec["color"]);
                }

                if (JsonArgs.Has(spec, "lineWeight"))
                {
                    var weight = spec["lineWeight"].Value<int>();
                    if (weight < 1 || weight > 16) throw new ArgumentException("lineWeight должен быть от 1 до 16");
                    subcategory.SetLineWeight(weight, GraphicsStyleType.Projection);
                }

                var material = JsonArgs.OptionalString(spec, "material");
                if (material != null) subcategory.Material = FamilyEditorIds.FindMaterial(doc, material);

                result.Add(new { name = subcategory.Name, id = subcategory.Id.GetValue(), created });
            }

            return new { category = parent.Name, subcategories = result };
        });
    }
}
