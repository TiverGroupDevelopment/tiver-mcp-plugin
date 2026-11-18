using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.ProjectModel;

namespace RevitMCPCommandSet.Services.ProjectModel;

/// <summary>Параметры экземпляров по имени (как edit_element_type) и разворот стен (Flip).</summary>
public class EditElementsEventHandler : ProjectModelEventHandlerBase
{
    public override string GetName() => "Edit elements";

    protected override object Run(UIApplication app, JObject args)
    {
        var doc = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var ids = JsonArgs.Ids(args, "ids");
        var parameters = args["parameters"] as JObject ?? new JObject();
        var flip = JsonArgs.OptionalBool(args, "flip", false);
        if (parameters.Count == 0 && !flip) throw new ArgumentException("Задайте parameters и/или flip: true");

        return InTransaction(doc, "MCP: изменить элементы", failures =>
        {
            var result = new List<object>();
            var errors = new List<string>();
            foreach (var id in ids)
            {
                var element = doc.GetElement(FamilyEditorIds.FromNumber(id)) ?? throw new ArgumentException($"Элемент {id} не найден");
                var applied = new List<object>();
                if (flip)
                {
                    if (element is Wall wall) wall.Flip();
                    else if (element is FamilyInstance fi && fi.CanFlipFacing) fi.flipFacing();
                    else errors.Add($"{id}: разворот не поддерживается для {element.GetType().Name}");
                }

                foreach (var property in parameters.Properties())
                {
                    try
                    {
                        ProjectModelUtils.SetParameter(doc, element, property.Name, property.Value);
                        doc.Regenerate();
                        applied.Add(ProjectModelUtils.Describe(doc, ProjectModelUtils.FindParameter(element, property.Name)));
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{id} {property.Name}: {ex.Message}");
                    }
                }

                doc.Regenerate();
                result.Add(new
                {
                    id,
                    name = element.Name,
                    category = element.Category?.Name,
                    flipped = (element as Wall)?.Flipped,
                    parameters = applied
                });
            }

            if (errors.Count > 0) throw new ArgumentException($"Изменения отменены: {string.Join(" | ", errors)}");
            return new { project = doc.Title, elements = result, warnings = failures.Warnings };
        });
    }
}
