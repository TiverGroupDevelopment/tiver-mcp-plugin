using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.ProjectModel;

namespace RevitMCPCommandSet.Services.ProjectModel;

public class GetCurtainWallInfoEventHandler : ProjectModelEventHandlerBase
{
    public override string GetName() => "Get curtain wall info";

    protected override object Run(UIApplication app, JObject args)
    {
        var doc = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var walls = ProjectModelUtils.ResolveWalls(doc, args);
        var details = JsonArgs.OptionalBool(args, "details", false);
        return new { project = doc.Title, walls = walls.Select(w => CurtainWallReader.Describe(doc, w, details)).ToList() };
    }
}
