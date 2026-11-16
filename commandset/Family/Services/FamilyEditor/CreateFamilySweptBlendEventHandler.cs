using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class CreateFamilySweptBlendEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family swept blend";

    protected override object Run(UIApplication app, JObject args)
    {
        var pathSpec = JsonArgs.Required(args, "path");
        var bottomSpec = JsonArgs.Required(args, "bottomProfile");
        var topSpec = JsonArgs.Required(args, "topProfile");
        var isSolid = JsonArgs.OptionalBool(args, "isSolid", true);

        return Edit(app, args, "MCP: create swept blend", doc =>
        {
            var plane = FamilyWorkPlane.Create(doc, args["workPlane"], "XY");
            var path = FamilyProfileBuilder.BuildPath(doc, plane, pathSpec);
            if (path.Size != 1)
            {
                throw new ArgumentException($"Траектория swept blend — одна кривая (отрезок из двух точек или одна дуга), получено кривых: {path.Size}");
            }

            var local = FamilyWorkPlane.ProfileLocal();
            var bottom = doc.Application.Create.NewCurveLoopsProfile(SingleLoop(doc, local, bottomSpec));
            var top = doc.Application.Create.NewCurveLoopsProfile(SingleLoop(doc, local, topSpec));

            var sweptBlend = doc.FamilyCreate.NewSweptBlend(isSolid, path.get_Item(0), plane.SketchPlane, bottom, top)
                             ?? throw new InvalidOperationException("Revit не создал swept blend");
            return FamilyFormUtils.Finish(doc, sweptBlend, args, plane);
        });
    }

    private static CurveArrArray SingleLoop(Document doc, FamilyWorkPlane plane, JToken loop)
    {
        var profile = new CurveArrArray();
        profile.Append(FamilyProfileBuilder.BuildLoop(doc, plane, loop));
        return profile;
    }
}
