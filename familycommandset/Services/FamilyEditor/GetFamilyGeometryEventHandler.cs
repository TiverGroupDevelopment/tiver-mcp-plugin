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
        var detail = JsonArgs.OptionalBool(args, "detail", false);
        var onlyIds = JsonArgs.Has(args, "elementIds") ? new HashSet<long>(JsonArgs.Ids(args, "elementIds")) : null;

        return Read(app, args, session =>
        {
            var doc = session.Document;
            var manager = doc.FamilyManager;

            var forms = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .ToElements()
                .OfType<GenericForm>()
                .Where(f => onlyIds == null || onlyIds.Contains(f.Id.GetValue()))
                .Select(f => detail
                    ? FamilyGeometryReader.DescribeDetailed(doc, manager, f)
                    : FamilyFormUtils.Describe(doc, manager, f))
                .ToList();

            var referencePlanes = new FilteredElementCollector(doc)
                .OfClass(typeof(ReferencePlane))
                .Cast<ReferencePlane>()
                .Select(FamilyDocInfo.DescribeReferencePlane)
                .ToList();

            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Select(l => new { id = l.Id.GetValue(), name = l.Name, elevationMm = FamilyDocInfo.Mm(l.Elevation) })
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
                constraints = FamilyConstraints.DescribeAll(doc),
                levels,
                views = FamilyDocInfo.Views(doc).Select(FamilyDocInfo.DescribeView).ToList(),
                materials
            };
        });
    }
}
