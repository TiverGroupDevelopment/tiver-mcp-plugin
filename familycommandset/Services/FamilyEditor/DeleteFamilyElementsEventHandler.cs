using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class DeleteFamilyElementsEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Delete family elements";

    protected override object Run(UIApplication app, JObject args)
    {
        var ids = JsonArgs.Ids(args, "elementIds");
        var force = JsonArgs.OptionalBool(args, "force", false);

        return Edit(app, args, "MCP: delete family elements", doc =>
        {
            var requested = new List<object>();
            var elementIds = new List<ElementId>();
            foreach (var id in ids)
            {
                var elementId = FamilyEditorIds.FromNumber(id);
                var element = doc.GetElement(elementId) ?? throw new ArgumentException($"Элемент {id} не найден");
                if (!force && !IsModelingElement(element))
                {
                    throw new InvalidOperationException(
                        $"Элемент {id} ({element.GetType().Name} '{element.Name}') не относится к формам, опорным плоскостям, размерам или линиям. Для удаления передайте force: true.");
                }

                requested.Add(new { id, kind = element.GetType().Name, name = element.Name });
                elementIds.Add(elementId);
            }

            var deleted = doc.Delete(elementIds).Select(e => e.GetValue()).ToList();
            doc.Regenerate();

            return new
            {
                requested,
                // Вместе с элементом Revit удаляет зависимые: размеры, выравнивания, эскизы.
                alsoDeleted = deleted.Where(d => !ids.Contains(d)).ToList()
            };
        });
    }

    private static bool IsModelingElement(Element element)
    {
        return element is GenericForm || element is ReferencePlane || element is Dimension
               || element is CurveElement || element is FamilyInstance;
    }
}
