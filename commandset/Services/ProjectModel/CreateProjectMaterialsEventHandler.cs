using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Services.ProjectModel;

public class CreateProjectMaterialsEventHandler : ProjectModelEventHandlerBase
{
    public override string GetName() => "Create project materials";

    protected override object Run(UIApplication app, JObject args)
    {
        var doc = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var specs = JsonArgs.RequiredArray(args, "materials").OfType<JObject>().ToList();
        if (specs.Count == 0) throw new ArgumentException("materials пуст");

        return InTransaction(doc, "MCP: материалы проекта", failures =>
        {
            var result = new List<object>();
            foreach (var spec in specs)
            {
                var name = JsonArgs.RequiredString(spec, "name");
                var material = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                    .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
                var created = material == null;
                if (material == null)
                {
                    var source = JsonArgs.OptionalString(spec, "duplicateFrom");
                    material = source != null
                        ? FamilyEditorIds.FindMaterial(doc, source).Duplicate(name)
                        : (Material)doc.GetElement(Material.Create(doc, name));
                }

                if (spec["color"] != null) material.Color = FamilyMaterials.ParseColor(spec["color"]);
                if (spec["transparency"] != null) material.Transparency = spec["transparency"].Value<int>();
                if (spec["materialClass"] != null) material.MaterialClass = spec["materialClass"].Value<string>();
                result.Add(new { id = material.Id.GetValue(), name = material.Name, created, color = new[] { (int)material.Color.Red, (int)material.Color.Green, (int)material.Color.Blue }, transparency = material.Transparency });
            }

            return new { project = doc.Title, materials = result, warnings = failures.Warnings };
        });
    }
}
