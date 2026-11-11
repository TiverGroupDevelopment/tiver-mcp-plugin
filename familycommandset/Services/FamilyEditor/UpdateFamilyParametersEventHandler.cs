using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>Свойства существующих параметров: имя, тип/экземпляр, отчётный, подсказка, удаление.</summary>
public class UpdateFamilyParametersEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Update family parameters";

    protected override object Run(UIApplication app, JObject args)
    {
        var specs = JsonArgs.RequiredArray(args, "parameters").OfType<JObject>().ToList();
        if (specs.Count == 0) throw new ArgumentException("parameters пуст");

        return Edit(app, args, "MCP: update family parameters", doc =>
        {
            var manager = doc.FamilyManager;
            var result = new List<object>();

            foreach (var spec in specs)
            {
                var name = JsonArgs.RequiredString(spec, "name");
                var parameter = FamilyParameterSpecs.Find(manager, name);
                var changed = new List<string>();

                if (JsonArgs.OptionalBool(spec, "remove", false))
                {
                    manager.RemoveParameter(parameter);
                    result.Add(new { name, changed = new List<string> { "removed" } });
                    continue;
                }

                if (JsonArgs.Has(spec, "isInstance"))
                {
                    var isInstance = spec["isInstance"].Value<bool>();
                    if (isInstance != parameter.IsInstance)
                    {
                        if (isInstance) manager.MakeInstance(parameter);
                        else manager.MakeType(parameter);
                        changed.Add("isInstance");
                    }
                }

                if (JsonArgs.Has(spec, "reporting"))
                {
                    var reporting = spec["reporting"].Value<bool>();
                    if (reporting != parameter.IsReporting)
                    {
                        if (reporting) manager.MakeReporting(parameter);
                        else manager.MakeNonReporting(parameter);
                        changed.Add("reporting");
                    }
                }

                if (JsonArgs.Has(spec, "description"))
                {
                    manager.SetDescription(parameter, spec["description"].Value<string>() ?? string.Empty);
                    changed.Add("description");
                }

                var rename = JsonArgs.OptionalString(spec, "rename");
                if (rename != null && !string.Equals(rename, parameter.Definition.Name, StringComparison.Ordinal))
                {
                    manager.RenameParameter(parameter, rename);
                    changed.Add("rename");
                }

                if (changed.Count == 0)
                {
                    throw new ArgumentException($"Для параметра '{name}' не задано ни одного изменения (rename, isInstance, reporting, description, remove)");
                }

                result.Add(new
                {
                    name = parameter.Definition.Name,
                    changed,
                    isInstance = parameter.IsInstance,
                    isReporting = parameter.IsReporting
                });
            }

            doc.Regenerate();
            return result;
        });
    }
}
