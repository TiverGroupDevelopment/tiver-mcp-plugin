using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class SetFamilyElementGraphicsEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Set family element graphics";

    protected override object Run(UIApplication app, JObject args)
    {
        var ids = JsonArgs.Ids(args, "elementIds");
        var subcategory = JsonArgs.OptionalString(args, "subcategory");
        var visibility = args["visibility"] as JObject;
        if (subcategory == null && visibility == null)
        {
            throw new ArgumentException("Задайте subcategory и/или visibility");
        }

        return Edit(app, args, "MCP: set family element graphics", doc =>
        {
            var elements = ids
                .Select(id => doc.GetElement(FamilyEditorIds.FromNumber(id)) ?? throw new ArgumentException($"Элемент {id} не найден"))
                .ToList();

            foreach (var element in elements)
            {
                if (subcategory != null) FamilyGraphics.SetSubcategory(doc, element, subcategory);
                if (visibility != null) FamilyGraphics.SetVisibility(element, visibility);
            }

            doc.Regenerate();
            return elements.Select(FamilyGraphics.Describe).ToList();
        });
    }
}
