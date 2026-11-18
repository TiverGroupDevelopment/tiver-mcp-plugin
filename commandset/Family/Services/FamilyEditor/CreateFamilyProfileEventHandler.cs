using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.ProjectModel;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>Контуры профиля линиями детализации в семействе профиля (Profile, Profile-Mullion).</summary>
public class CreateFamilyProfileEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family profile";

    protected override object Run(UIApplication app, JObject args)
    {
        var loops = JsonArgs.Required(args, "loops");
        var clear = JsonArgs.OptionalBool(args, "clear", true);

        return Edit(app, args, "MCP: профиль", doc =>
        {
            if (doc.OwnerFamily?.FamilyCategory?.Id.GetValue() != (long)BuiltInCategory.OST_ProfileFamilies)
            {
                throw new InvalidOperationException($"Семейство категории '{doc.OwnerFamily?.FamilyCategory?.Name}' — не профиль. Создайте его по шаблону профиля (Metric Profile-Mullion.rft / Метрический профиль импоста.rft)");
            }

            var view = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().FirstOrDefault(v => !v.IsTemplate)
                       ?? throw new InvalidOperationException("В семействе профиля нет плана для линий");

            var removed = 0;
            if (clear)
            {
                var old = new FilteredElementCollector(doc).OfClass(typeof(CurveElement)).Cast<CurveElement>()
                    .Where(c => c.CurveElementType == CurveElementType.DetailCurve).Select(c => c.Id).ToList();
                if (old.Count > 0) removed = doc.Delete(old).Count;
            }

            var profile = FamilyProfileBuilder.BuildProfile(doc, FamilyWorkPlane.ProfileLocal(), loops);
            var lines = 0;
            var min = new XYZ(double.MaxValue, double.MaxValue, 0);
            var max = new XYZ(double.MinValue, double.MinValue, 0);
            var loopIndex = 0;
            var loopInfo = new List<object>();
            foreach (CurveArray loop in profile)
            {
                var segments = 0;
                foreach (Curve curve in loop)
                {
                    doc.FamilyCreate.NewDetailCurve(view, curve);
                    lines++;
                    segments++;
                    foreach (var p in new[] { curve.GetEndPoint(0), curve.GetEndPoint(1) })
                    {
                        min = new XYZ(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), 0);
                        max = new XYZ(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), 0);
                    }
                }

                loopInfo.Add(new { loop = loopIndex++, role = loopIndex == 1 ? "outer" : "inner", segments });
            }

            return new
            {
                view = view.Name,
                removedLines = removed,
                lines,
                loops = loopInfo,
                boundsMm = new { min = new[] { ProjectModelUtils.Mm(min.X), ProjectModelUtils.Mm(min.Y) }, max = new[] { ProjectModelUtils.Mm(max.X), ProjectModelUtils.Mm(max.Y) } }
            };
        });
    }
}
