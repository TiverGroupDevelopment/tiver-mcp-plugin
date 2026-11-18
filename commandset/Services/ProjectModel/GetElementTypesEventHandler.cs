using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.ProjectModel;

namespace RevitMCPCommandSet.Services.ProjectModel;

public class GetElementTypesEventHandler : ProjectModelEventHandlerBase
{
    public override string GetName() => "Get element types";

    protected override object Run(UIApplication app, JObject args)
    {
        var doc = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var kind = JsonArgs.OptionalString(args, "kind") ?? "any";
        var name = JsonArgs.OptionalString(args, "name");
        var withParameters = JsonArgs.OptionalBool(args, "includeParameters", name != null);

        var types = ProjectModelUtils.TypesOfKind(doc, kind);
        if (name != null) types = types.Where(t => ProjectModelUtils.NameMatches(t, name)).ToList();
        if (types.Count > 300) types = types.Take(300).ToList();

        return new
        {
            project = doc.Title,
            kind,
            count = types.Count,
            types = types.OrderBy(ProjectModelUtils.FullName).Select(t => new
            {
                id = t.Id.GetValue(),
                name = t.Name,
                family = t.FamilyName,
                fullName = ProjectModelUtils.FullName(t),
                @class = t.GetType().Name,
                category = t.Category?.Name,
                parameters = withParameters
                    ? t.Parameters.Cast<Parameter>().Where(p => p.Definition != null).OrderBy(p => p.Definition.Name).Select(p => ProjectModelUtils.Describe(doc, p)).ToList()
                    : null
            }).ToList()
        };
    }
}
