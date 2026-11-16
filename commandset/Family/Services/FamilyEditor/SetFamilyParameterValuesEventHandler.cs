using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class SetFamilyParameterValuesEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Set family parameter values";

    protected override object Run(UIApplication app, JObject args)
    {
        var values = JsonArgs.RequiredObject(args, "values");
        if (!values.Properties().Any()) throw new ArgumentException("values пуст");

        return Edit(app, args, "MCP: set family parameter values", doc =>
        {
            var manager = doc.FamilyManager;
            var types = ResolveTypes(manager, args);
            var parameters = values.Properties()
                .Select(p => (parameter: FamilyParameterSpecs.Find(manager, p.Name), value: p.Value))
                .ToList();

            var original = manager.CurrentType;
            try
            {
                foreach (var type in types)
                {
                    manager.CurrentType = type;
                    foreach (var (parameter, value) in parameters)
                    {
                        FamilyParameterSpecs.SetCurrentTypeValue(doc, manager, parameter, value);
                    }
                }
            }
            finally
            {
                if (original != null) manager.CurrentType = original;
            }

            return parameters.Select(p => FamilyParameterSpecs.Describe(doc, p.parameter, types)).ToList();
        });
    }

    private static List<FamilyType> ResolveTypes(FamilyManager manager, JObject args)
    {
        if (JsonArgs.OptionalBool(args, "allTypes", false)) return FamilyParameterSpecs.AllTypes(manager);

        if (JsonArgs.Has(args, "typeNames"))
        {
            return JsonArgs.RequiredArray(args, "typeNames")
                .Select(t => FamilyParameterSpecs.FindType(manager, t.Value<string>()))
                .ToList();
        }

        var current = manager.CurrentType ?? throw new InvalidOperationException("Нет текущего типоразмера — создайте его через add_family_type");
        return new List<FamilyType> { current };
    }
}
