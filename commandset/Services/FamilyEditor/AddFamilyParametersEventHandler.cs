using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Services.FamilyEditor;

public class AddFamilyParametersEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Add family parameters";

    protected override object Run(UIApplication app, JObject args)
    {
        var definitions = JsonArgs.RequiredArray(args, "parameters").OfType<JObject>().ToList();
        if (definitions.Count == 0) throw new ArgumentException("parameters пуст");

        return Edit(app, args, "MCP: add family parameters", doc =>
        {
            var manager = doc.FamilyManager;
            var types = FamilyParameterSpecs.AllTypes(manager);
            var created = new List<FamilyParameter>();

            foreach (var definition in definitions)
            {
                var name = JsonArgs.RequiredString(definition, "name");
                if (manager.Parameters.Cast<FamilyParameter>().Any(p => string.Equals(p.Definition.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ArgumentException($"Параметр '{name}' уже существует");
                }

                var kind = FamilyParameterSpecs.ParseKind(JsonArgs.OptionalString(definition, "kind") ?? "Length");
                var group = JsonArgs.OptionalString(definition, "group") ?? FamilyParameterSpecs.DefaultGroup(kind);
                var parameter = FamilyParameterSpecs.Add(manager, name, kind, group, JsonArgs.OptionalBool(definition, "isInstance", false));

                if (JsonArgs.Has(definition, "value"))
                {
                    if (types.Count == 0) throw new InvalidOperationException("В семействе нет типоразмеров — создайте его через add_family_type");
                    FamilyParameterSpecs.SetForTypes(doc, manager, parameter, definition["value"], types);
                }

                created.Add(parameter);
            }

            foreach (var definition in definitions)
            {
                var formula = JsonArgs.OptionalString(definition, "formula");
                if (formula == null) continue;
                var parameter = FamilyParameterSpecs.Find(manager, JsonArgs.RequiredString(definition, "name"));
                manager.SetFormula(parameter, formula);
            }

            return created.Select(p => FamilyParameterSpecs.Describe(doc, p, types)).ToList();
        });
    }
}
