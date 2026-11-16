using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class SetFamilyParameterFormulasEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Set family parameter formulas";

    protected override object Run(UIApplication app, JObject args)
    {
        var formulas = JsonArgs.RequiredObject(args, "formulas");
        if (!formulas.Properties().Any()) throw new ArgumentException("formulas пуст");

        return Edit(app, args, "MCP: set family parameter formulas", doc =>
        {
            var manager = doc.FamilyManager;
            var touched = new List<Autodesk.Revit.DB.FamilyParameter>();

            foreach (var property in formulas.Properties())
            {
                var parameter = FamilyParameterSpecs.Find(manager, property.Name);
                var formula = property.Value.Type == JTokenType.Null ? null : property.Value.Value<string>()?.Trim();

                if (string.IsNullOrEmpty(formula))
                {
                    manager.SetFormula(parameter, null);
                }
                else
                {
                    if (!parameter.CanAssignFormula)
                    {
                        throw new InvalidOperationException($"Параметру '{parameter.Definition.Name}' нельзя назначить формулу");
                    }

                    manager.SetFormula(parameter, formula);
                }

                touched.Add(parameter);
            }

            var types = FamilyParameterSpecs.AllTypes(manager);
            return touched.Select(p => FamilyParameterSpecs.Describe(doc, p, types)).ToList();
        });
    }
}
