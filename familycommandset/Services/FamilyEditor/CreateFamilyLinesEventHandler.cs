using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class CreateFamilyLinesEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family lines";

    protected override object Run(UIApplication app, JObject args)
    {
        var pathSpec = JsonArgs.Required(args, "path");
        var kind = (JsonArgs.OptionalString(args, "kind") ?? "model").ToLowerInvariant();
        if (kind != "model" && kind != "symbolic")
        {
            throw new ArgumentException($"Неизвестный kind '{kind}'. Допустимо: model, symbolic");
        }

        var subcategory = JsonArgs.OptionalString(args, "subcategory");
        var visibility = args["visibility"] as JObject;

        return Edit(app, args, $"MCP: create {kind} lines", doc =>
        {
            var plane = FamilyWorkPlane.Create(doc, args["workPlane"], "XY");
            var curves = FamilyProfileBuilder.BuildPath(doc, plane, pathSpec);
            var created = new List<Element>();

            foreach (Curve curve in curves)
            {
                Element line = kind == "model"
                    ? (Element)doc.FamilyCreate.NewModelCurve(curve, plane.SketchPlane)
                    : doc.FamilyCreate.NewSymbolicCurve(curve, plane.SketchPlane);
                if (line == null) throw new InvalidOperationException("Revit не создал линию");

                if (subcategory != null) FamilyGraphics.SetSubcategory(doc, line, subcategory);
                if (visibility != null) FamilyGraphics.SetVisibility(line, visibility);
                created.Add(line);
            }

            doc.Regenerate();
            return new
            {
                kind,
                workPlane = plane.Describe(),
                lines = created.Select(FamilyGraphics.Describe).ToList()
            };
        });
    }
}
