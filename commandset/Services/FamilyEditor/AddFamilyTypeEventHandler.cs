using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Services.FamilyEditor;

public class AddFamilyTypeEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Add family type";

    protected override object Run(UIApplication app, JObject args)
    {
        var name = JsonArgs.RequiredString(args, "name");

        return Edit(app, args, "MCP: add family type", doc =>
        {
            var manager = doc.FamilyManager;
            if (FamilyParameterSpecs.AllTypes(manager).Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"Типоразмер '{name}' уже существует");
            }

            var baseType = JsonArgs.OptionalString(args, "baseType");
            if (baseType != null) manager.CurrentType = FamilyParameterSpecs.FindType(manager, baseType);

            var created = manager.NewType(name);
            manager.CurrentType = created;

            var described = new List<object>();
            if (JsonArgs.Has(args, "values"))
            {
                foreach (var property in JsonArgs.RequiredObject(args, "values").Properties())
                {
                    var parameter = FamilyParameterSpecs.Find(manager, property.Name);
                    FamilyParameterSpecs.SetCurrentTypeValue(doc, manager, parameter, property.Value);
                    described.Add(FamilyParameterSpecs.Describe(doc, parameter, new List<Autodesk.Revit.DB.FamilyType> { created }));
                }
            }

            return new
            {
                type = created.Name,
                copiedFrom = baseType,
                types = FamilyParameterSpecs.AllTypes(manager).Select(t => t.Name).ToList(),
                values = described
            };
        });
    }
}
