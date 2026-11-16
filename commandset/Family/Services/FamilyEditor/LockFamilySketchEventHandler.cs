using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Закрепляет линии эскиза форм (выдавливание, вращение) и ось вращения на опорных плоскостях — см. FamilySketchLocker.
/// Для каждой формы задаётся список плоскостей, к которым её эскиз можно закреплять: линия закрепляется на тех из них,
/// в которых она лежит. Незакреплённые прямые возвращаются в free (наклонные линии фасок закрепить нельзя).
/// </summary>
public class LockFamilySketchEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Lock family sketch";

    protected override object Run(UIApplication app, JObject args)
    {
        var specs = JsonArgs.RequiredArray(args, "forms").OfType<JObject>().ToList();
        if (specs.Count == 0) throw new ArgumentException("forms пуст");
        var toleranceMm = JsonArgs.OptionalDouble(args, "toleranceMm", FamilySketchLocker.DefaultToleranceFeet * 304.8);

        return Edit(app, args, "MCP: lock family sketch", doc =>
        {
            var forms = new List<GenericForm>();
            var names = new List<HashSet<string>>();
            var axes = new List<bool>();
            foreach (var spec in specs)
            {
                var id = JsonArgs.Required(spec, "elementId").Value<long>();
                var form = doc.GetElement(FamilyEditorIds.FromNumber(id)) as GenericForm
                           ?? throw new ArgumentException($"Форма {id} не найдена");
                var planes = JsonArgs.RequiredArray(spec, "planes").Select(t => t.Value<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (planes.Count == 0) throw new ArgumentException($"Форма {id}: planes пуст");
                forms.Add(form);
                names.Add(new HashSet<string>(planes));
                axes.Add(JsonArgs.OptionalBool(spec, "axis", true));
            }

            var result = FamilySketchLocker.Lock(doc, forms, names, axes, toleranceMm / 304.8);
            doc.Regenerate();
            return result;
        });
    }
}
