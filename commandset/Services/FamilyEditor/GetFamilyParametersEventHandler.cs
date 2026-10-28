using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;
using RevitMCPCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Services.FamilyEditor;

public class GetFamilyParametersEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Get family parameters";

    protected override object Run(UIApplication app, JObject args)
    {
        return Read(app, args, session =>
        {
            var doc = session.Document;
            var manager = doc.FamilyManager;
            var nameFilter = JsonArgs.OptionalString(args, "nameFilter");
            var typeName = JsonArgs.OptionalString(args, "typeName");
            var includeBuiltIn = JsonArgs.OptionalBool(args, "includeBuiltIn", false);

            var types = typeName == null
                ? FamilyParameterSpecs.AllTypes(manager)
                : new List<Autodesk.Revit.DB.FamilyType> { FamilyParameterSpecs.FindType(manager, typeName) };

            var parameters = manager.Parameters
                .Cast<Autodesk.Revit.DB.FamilyParameter>()
                .Where(p => includeBuiltIn || p.Id.GetValue() > 0)
                .Where(p => nameFilter == null || p.Definition.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(p => p.Definition.Name)
                .Select(p => FamilyParameterSpecs.Describe(doc, p, types))
                .ToList();

            return new
            {
                category = doc.OwnerFamily?.FamilyCategory?.Name,
                currentType = manager.CurrentType?.Name,
                types = FamilyParameterSpecs.AllTypes(manager).Select(t => t.Name).ToList(),
                units = "Length: mm, Area: m², Volume: m³, Angle: degrees",
                parameters
            };
        });
    }
}
