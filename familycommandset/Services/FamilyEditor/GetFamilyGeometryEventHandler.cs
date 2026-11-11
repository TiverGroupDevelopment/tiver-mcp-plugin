using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class GetFamilyGeometryEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Get family geometry";

    protected override object Run(UIApplication app, JObject args)
    {
        return Read(app, args, session =>
        {
            var doc = session.Document;
            var manager = doc.FamilyManager;

            var forms = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements()
                .OfType<GenericForm>()
                .Select(f => FamilyFormUtils.Describe(doc, manager, f))
                .ToList();

            var referencePlanes = new FilteredElementCollector(doc)
                .OfClass(typeof(ReferencePlane))
                .Cast<ReferencePlane>()
                .Select(r =>
                {
                    var plane = r.GetPlane();
                    return new
                    {
                        id = r.Id.GetValue(),
                        name = r.Name,
                        originMm = new[] { Mm(plane.Origin.X), Mm(plane.Origin.Y), Mm(plane.Origin.Z) },
                        normal = new[] { Math.Round(plane.Normal.X, 6), Math.Round(plane.Normal.Y, 6), Math.Round(plane.Normal.Z, 6) }
                    };
                })
                .ToList();

            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Select(l => new { id = l.Id.GetValue(), name = l.Name, elevationMm = Mm(l.Elevation) })
                .ToList();

            var materials = new FilteredElementCollector(doc)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .Select(m => m.Name)
                .OrderBy(n => n)
                .ToList();

            return new
            {
                category = doc.OwnerFamily?.FamilyCategory?.Name,
                forms,
                referencePlanes,
                levels,
                materials
            };
        });
    }

    private static double Mm(double feet)
    {
        return Math.Round(feet * 304.8, 3);
    }
}
