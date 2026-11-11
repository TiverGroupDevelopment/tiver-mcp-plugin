using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class CreateFamilyBlendEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family blend";

    protected override object Run(UIApplication app, JObject args)
    {
        var baseSpec = JsonArgs.Required(args, "baseProfile");
        var topSpec = JsonArgs.Required(args, "topProfile");
        var heightMm = JsonArgs.RequiredDouble(args, "height");
        if (heightMm < 0.5) throw new ArgumentException("height должна быть не меньше 0.5 мм");
        var isSolid = JsonArgs.OptionalBool(args, "isSolid", true);

        return Edit(app, args, "MCP: create blend", doc =>
        {
            var plane = FamilyWorkPlane.Create(doc, args["workPlane"], "XY");
            var baseLoop = FamilyProfileBuilder.BuildLoop(doc, plane, baseSpec);
            var topLoop = FamilyProfileBuilder.BuildLoop(doc, plane, topSpec, heightMm);
            var blend = doc.FamilyCreate.NewBlend(isSolid, topLoop, baseLoop, plane.SketchPlane);
            blend.BottomOffset = 0;
            blend.TopOffset = heightMm / 304.8;
            return FamilyFormUtils.Finish(doc, blend, args, plane);
        });
    }
}
