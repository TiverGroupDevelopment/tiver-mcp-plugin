using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Services.FamilyEditor;

public class AssociateFamilyParametersEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Associate family parameters";

    protected override object Run(UIApplication app, JObject args)
    {
        var associations = JsonArgs.RequiredArray(args, "associations").OfType<JObject>().ToList();
        if (associations.Count == 0) throw new ArgumentException("associations пуст");

        return Edit(app, args, "MCP: associate family parameters", doc =>
        {
            var manager = doc.FamilyManager;
            var touched = new Dictionary<long, GenericForm>();

            foreach (var association in associations)
            {
                var elementId = JsonArgs.Required(association, "elementId").Value<long>();
                var form = FamilyFormUtils.GetForm(doc, elementId);
                var elementParameterName = JsonArgs.RequiredString(association, "elementParameter");
                var elementParameter = FindElementParameter(form, elementParameterName);

                if (!manager.CanElementParameterBeAssociated(elementParameter))
                {
                    throw new InvalidOperationException($"Параметр '{elementParameterName}' элемента {elementId} нельзя связать с параметром семейства");
                }

                var familyParameterName = JsonArgs.OptionalString(association, "familyParameter");
                var familyParameter = familyParameterName == null ? null : FamilyParameterSpecs.Find(manager, familyParameterName);
                manager.AssociateElementParameterToFamilyParameter(elementParameter, familyParameter);
                touched[elementId] = form;
            }

            doc.Regenerate();
            return touched.Values.Select(f => FamilyFormUtils.Describe(doc, manager, f)).ToList();
        });
    }

    private static Parameter FindElementParameter(Element element, string name)
    {
        if (Enum.TryParse(name, true, out BuiltInParameter builtIn) && !int.TryParse(name, out _))
        {
            var byBuiltIn = element.get_Parameter(builtIn);
            if (byBuiltIn != null) return byBuiltIn;
        }

        return element.LookupParameter(name)
               ?? throw new ArgumentException($"У элемента {element.Id} нет параметра '{name}'. Список доступных — в get_family_geometry → associableParameters");
    }
}
