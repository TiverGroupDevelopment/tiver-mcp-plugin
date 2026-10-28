using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Services.FamilyEditor;

public class CreateFamilyRevolutionEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family revolution";

    protected override object Run(UIApplication app, JObject args)
    {
        var profileSpec = JsonArgs.Required(args, "profile");
        var axisSpec = JsonArgs.RequiredObject(args, "axis");
        var startAngle = JsonArgs.OptionalDouble(args, "startAngle", 0);
        var endAngle = JsonArgs.OptionalDouble(args, "endAngle", 360);
        if (endAngle - startAngle <= 0 || endAngle - startAngle > 360)
        {
            throw new ArgumentException("Требуется 0 < endAngle - startAngle ≤ 360");
        }

        var isSolid = JsonArgs.OptionalBool(args, "isSolid", true);

        return Edit(app, args, "MCP: create revolution", doc =>
        {
            var plane = FamilyWorkPlane.Create(doc, args["workPlane"], "XZ");
            var profile = FamilyProfileBuilder.BuildProfile(doc, plane, profileSpec);
            var axis = Line.CreateBound(
                plane.ToWorld(axisSpec["start"] ?? throw new ArgumentException("axis.start не задан")),
                plane.ToWorld(axisSpec["end"] ?? throw new ArgumentException("axis.end не задан")));
            var revolution = doc.FamilyCreate.NewRevolution(
                isSolid, profile, plane.SketchPlane, axis, startAngle * Math.PI / 180.0, endAngle * Math.PI / 180.0);
            return FamilyFormUtils.Finish(doc, revolution, args, plane);
        });
    }
}
