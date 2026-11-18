using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.ProjectModel;

namespace RevitMCPCommandSet.Services.ProjectModel;

/// <summary>
/// Слои базовой стены (структура типа): копия типа и список слоёв снаружи внутрь — функция, материал, толщина.
/// Например, утеплитель и несущий слой, чтобы в модели была реальная толщина стены.
/// </summary>
public class SetWallLayersEventHandler : ProjectModelEventHandlerBase
{
    public override string GetName() => "Set wall layers";

    protected override object Run(UIApplication app, JObject args)
    {
        var doc = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var source = ProjectModelUtils.FindType(doc, JsonArgs.RequiredString(args, "type"), "wall") as WallType
                     ?? throw new ArgumentException("type — тип базовой стены");
        var duplicateAs = JsonArgs.OptionalString(args, "duplicateAs");
        var specs = JsonArgs.RequiredArray(args, "layers").OfType<JObject>().ToList();
        if (specs.Count == 0) throw new ArgumentException("layers пуст");

        return InTransaction(doc, $"MCP: слои стены {duplicateAs ?? source.Name}", failures =>
        {
            var type = source;
            if (duplicateAs != null)
            {
                type = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>()
                           .FirstOrDefault(t => string.Equals(t.Name, duplicateAs, StringComparison.OrdinalIgnoreCase))
                       ?? (WallType)source.Duplicate(duplicateAs);
            }

            var layers = new List<CompoundStructureLayer>();
            var createdMaterials = new List<string>();
            foreach (var spec in specs)
            {
                var functionName = JsonArgs.OptionalString(spec, "function") ?? "Structure";
                if (!Enum.TryParse(functionName, true, out MaterialFunctionAssignment function))
                {
                    throw new ArgumentException($"function '{functionName}': Structure, Substrate, Insulation, Finish1, Finish2, Membrane");
                }

                var thickness = JsonArgs.RequiredDouble(spec, "thicknessMm") / ProjectModelUtils.MmPerFoot;
                var materialName = JsonArgs.OptionalString(spec, "material");
                var materialId = ElementId.InvalidElementId;
                if (materialName != null)
                {
                    var material = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                        .FirstOrDefault(m => string.Equals(m.Name, materialName, StringComparison.OrdinalIgnoreCase));
                    if (material == null)
                    {
                        material = (Material)doc.GetElement(Material.Create(doc, materialName));
                        if (spec["color"] != null) material.Color = FamilyMaterials.ParseColor(spec["color"]);
                        createdMaterials.Add(materialName);
                    }

                    materialId = material.Id;
                }

                layers.Add(new CompoundStructureLayer(thickness, function, materialId));
            }

            var structure = CompoundStructure.CreateSimpleCompoundStructure(layers);
            var core = layers.FindIndex(l => l.Function == MaterialFunctionAssignment.Structure);
            if (core >= 0)
            {
                structure.SetNumberOfShellLayers(ShellLayerType.Exterior, core);
                structure.SetNumberOfShellLayers(ShellLayerType.Interior, layers.Count - core - 1);
            }

            type.SetCompoundStructure(structure);
            doc.Regenerate();
            return new
            {
                project = doc.Title,
                type = type.Name,
                widthMm = ProjectModelUtils.Mm(type.Width),
                layers = type.GetCompoundStructure().GetLayers().Select(l => new
                {
                    function = l.Function.ToString(),
                    material = doc.GetElement(l.MaterialId)?.Name,
                    thicknessMm = ProjectModelUtils.Mm(l.Width)
                }).ToList(),
                note = "Слои перечислены снаружи внутрь; ось стены — по середине общей толщины",
                createdMaterials
            };
        });
    }
}
