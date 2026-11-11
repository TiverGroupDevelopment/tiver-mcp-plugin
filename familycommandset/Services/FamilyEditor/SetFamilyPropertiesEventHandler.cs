using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class SetFamilyPropertiesEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Set family properties";

    protected override object Run(UIApplication app, JObject args)
    {
        return Edit(app, args, "MCP: set family properties", doc =>
        {
            var changed = FamilyProperties.Apply(doc, args);
            if (changed.Count == 0)
            {
                throw new ArgumentException($"Не задано ни одного свойства. Допустимо: category, {string.Join(", ", FamilyProperties.WritableKeys)}");
            }

            doc.Regenerate();
            return new { changed, properties = FamilyProperties.Describe(doc) };
        });
    }
}
