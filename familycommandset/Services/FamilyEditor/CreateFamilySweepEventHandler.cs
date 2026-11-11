using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class CreateFamilySweepEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family sweep";

    protected override object Run(UIApplication app, JObject args)
    {
        var pathSpec = JsonArgs.Required(args, "path");
        var profileSpec = JsonArgs.Required(args, "profile");
        var isSolid = JsonArgs.OptionalBool(args, "isSolid", true);
        var profileAt = (JsonArgs.OptionalString(args, "profileLocation") ?? "start").ToLowerInvariant();

        return Edit(app, args, "MCP: create sweep", doc =>
        {
            var plane = FamilyWorkPlane.Create(doc, args["workPlane"], "XY");
            var path = FamilyProfileBuilder.BuildPath(doc, plane, pathSpec);
            var profile = doc.Application.Create.NewCurveLoopsProfile(
                FamilyProfileBuilder.BuildProfile(doc, FamilyWorkPlane.ProfileLocal(), profileSpec));

            ProfilePlaneLocation location;
            switch (profileAt)
            {
                case "start":
                    location = ProfilePlaneLocation.Start;
                    break;
                case "end":
                    location = ProfilePlaneLocation.End;
                    break;
                case "middle":
                case "midpoint":
                    location = ProfilePlaneLocation.MidPoint;
                    break;
                default:
                    throw new ArgumentException($"Неизвестное profileLocation '{profileAt}'. Допустимо: start, middle, end");
            }

            var curveIndex = (int)JsonArgs.OptionalDouble(args, "profileCurveIndex", 0);
            if (curveIndex < 0 || curveIndex >= path.Size)
            {
                throw new ArgumentException($"profileCurveIndex {curveIndex} вне диапазона: в траектории {path.Size} кривых");
            }

            var sweep = doc.FamilyCreate.NewSweep(isSolid, path, plane.SketchPlane, profile, curveIndex, location)
                        ?? throw new InvalidOperationException("Revit не создал sweep");
            return FamilyFormUtils.Finish(doc, sweep, args, plane);
        });
    }
}
