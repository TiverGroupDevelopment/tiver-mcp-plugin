using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;
using RevitMCPCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.Topography;

namespace RevitMCPCommandSet.Services.Topography;

public class GetTopographyEventHandler : TopographyEventHandlerBase
{
    public override string GetName() => "Get topography";

    protected override object Run(Document doc, JObject args)
    {
        var includePoints = JsonArgs.OptionalBool(args, "includePoints", false);
        var maxPoints = (int)JsonArgs.OptionalDouble(args, "maxPoints", 1000);

        var elements = JsonArgs.Has(args, "elementId")
            ? new List<Element> { TopoUtils.Resolve(doc, args) }
            : TopoUtils.Collect(doc);

        return new
        {
            count = elements.Count,
            toposolidSupported = ToposolidSupported,
            items = elements.Select(e => Describe(doc, e, includePoints, maxPoints)).ToList()
        };
    }

#if REVIT2024_OR_GREATER
    private const bool ToposolidSupported = true;
#else
    private const bool ToposolidSupported = false;
#endif

    private static object Describe(Document doc, Element element, bool includePoints, int maxPoints)
    {
        List<XYZ> points;
        int? boundaryPointCount = null;
        long? hostId = null;
        List<long> childIds;
        string levelName = null;
        double? heightOffsetMm = null;
        double? areaM2;
        double? surfaceAreaM2 = null;
        List<object> vertexDetails = null;

        switch (element)
        {
            case TopographySurface surface:
                points = surface.GetPoints().ToList();
                if (!surface.IsSiteSubRegion) boundaryPointCount = surface.GetBoundaryPoints().Count;
                if (surface.IsSiteSubRegion) hostId = surface.AsSiteSubRegion().HostId.GetValue();
                childIds = surface.IsSiteSubRegion
                    ? new List<long>()
                    : surface.GetHostedSubRegionIds().Select(id => id.GetValue()).ToList();
                areaM2 = TopoUtils.AreaM2(surface, BuiltInParameter.PROJECTED_SURFACE_AREA);
                surfaceAreaM2 = TopoUtils.AreaM2(surface, BuiltInParameter.SURFACE_AREA);
                break;
#if REVIT2024_OR_GREATER
            case Toposolid solid:
                var vertices = ToposolidShape.ReadVertices(solid);
                points = vertices.Select(v => v.Position).ToList();
                if (includePoints)
                {
                    vertexDetails = vertices.Take(maxPoints)
                        .Select(v => (object)new { position = TopoUtils.Mm(v.Position), type = v.VertexType.ToString() })
                        .ToList();
                }

                if (solid.HostTopoId != ElementId.InvalidElementId) hostId = solid.HostTopoId.GetValue();
                childIds = solid.GetSubDivisionIds().Select(id => id.GetValue()).ToList();
                levelName = doc.GetElement(solid.LevelId)?.Name;
                var offset = solid.get_Parameter(BuiltInParameter.TOPOSOLID_HEIGHTABOVELEVEL_PARAM);
                if (offset != null && offset.HasValue) heightOffsetMm = TopoUtils.Mm(offset.AsDouble());
                areaM2 = TopoUtils.AreaM2(solid, BuiltInParameter.HOST_AREA_COMPUTED);
                break;
#endif
            default:
                throw new ArgumentException($"Элемент {element.Id.GetValue()} не является рельефом");
        }

        var box = element.get_BoundingBox(null);

        return new
        {
            id = element.Id.GetValue(),
            kind = TopoUtils.KindOf(element),
            name = element.Name,
            typeName = doc.GetElement(element.GetTypeId())?.Name,
            material = TopoUtils.MaterialName(doc, element),
            level = levelName,
            heightOffsetMm,
            hostId,
            childIds,
            pointCount = points.Count,
            boundaryPointCount,
            elevationRangeMm = points.Count == 0
                ? null
                : new { min = TopoUtils.Mm(points.Min(p => p.Z)), max = TopoUtils.Mm(points.Max(p => p.Z)) },
            boundingBoxMm = box == null ? null : new { min = TopoUtils.Mm(box.Min), max = TopoUtils.Mm(box.Max) },
            projectedAreaM2 = areaM2,
            surfaceAreaM2,
            pointsTruncated = includePoints && points.Count > maxPoints,
            points = includePoints
                ? vertexDetails ?? points.Take(maxPoints).Select(p => (object)TopoUtils.Mm(p)).ToList()
                : null
        };
    }
}
