using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Services.FamilyEditor;

public class CreateFamilyExtrusionEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family extrusion";

    protected override object Run(UIApplication app, JObject args)
    {
        var profileSpec = JsonArgs.Required(args, "profile");
        var startMm = JsonArgs.OptionalDouble(args, "start", 0);
        var endMm = JsonArgs.RequiredDouble(args, "end");
        if (Math.Abs(endMm - startMm) < 0.5) throw new ArgumentException("Глубина выдавливания (end - start) должна быть не меньше 0.5 мм");
        var isSolid = JsonArgs.OptionalBool(args, "isSolid", true);

        return Edit(app, args, "MCP: create extrusion", doc =>
        {
            var plane = FamilyWorkPlane.Create(doc, args["workPlane"], "XY");
            var profile = FamilyProfileBuilder.BuildProfile(doc, plane, profileSpec);
            var extrusion = doc.FamilyCreate.NewExtrusion(isSolid, profile, plane.SketchPlane, endMm / 304.8);
            extrusion.StartOffset = startMm / 304.8;
            extrusion.EndOffset = endMm / 304.8;
            return FamilyFormUtils.Finish(doc, extrusion, args, plane);
        });
    }
}
