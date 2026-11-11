using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class CreateFamilyMaterialsEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family materials";

    protected override object Run(UIApplication app, JObject args)
    {
        var specs = JsonArgs.RequiredArray(args, "materials").OfType<JObject>().ToList();
        if (specs.Count == 0) throw new ArgumentException("materials пуст");

        return Edit(app, args, "MCP: create family materials", doc =>
        {
            var result = new List<object>();
            foreach (var spec in specs)
            {
                var name = JsonArgs.RequiredString(spec, "name");
                var existing = new FilteredElementCollector(doc)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                var created = existing == null;
                Material material;
                if (existing != null)
                {
                    material = existing;
                }
                else
                {
                    var source = JsonArgs.OptionalString(spec, "duplicateFrom");
                    material = source != null
                        ? FamilyEditorIds.FindMaterial(doc, source).Duplicate(name)
                        : (Material)doc.GetElement(Material.Create(doc, name));
                }

                if (JsonArgs.Has(spec, "color")) material.Color = FamilyMaterials.ParseColor(spec["color"]);

                if (JsonArgs.Has(spec, "transparency"))
                {
                    var transparency = spec["transparency"].Value<int>();
                    if (transparency < 0 || transparency > 100) throw new ArgumentException("transparency должна быть от 0 до 100");
                    material.Transparency = transparency;
                }

                var materialClass = JsonArgs.OptionalString(spec, "materialClass");
                if (materialClass != null) material.MaterialClass = materialClass;

                result.Add(new { created, material = FamilyMaterials.Describe(material) });
            }

            return result;
        });
    }
}
