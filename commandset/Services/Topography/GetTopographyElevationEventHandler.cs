using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;
using RevitMCPCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.Topography;

namespace RevitMCPCommandSet.Services.Topography;

public class GetTopographyElevationEventHandler : TopographyEventHandlerBase
{
    public override string GetName() => "Get topography elevation";

    protected override object Run(Document doc, JObject args)
    {
        var points = TopoUtils.ParsePoints2(args, "points");
        var elements = JsonArgs.Has(args, "elementId")
            ? new List<Element> { TopoUtils.Resolve(doc, args) }
            : TopoUtils.Collect(doc);
        if (elements.Count == 0) throw new InvalidOperationException("В проекте нет топоповерхностей");

        var samplers = elements.Select(e => new TopoSurfaceSampler(e)).ToList();

        var results = points.Select(point =>
        {
            TopoSurfaceSampler hit = null;
            double? best = null;
            foreach (var sampler in samplers)
            {
                var z = sampler.ElevationAt(point.X, point.Y);
                if (z.HasValue && (!best.HasValue || z.Value > best.Value))
                {
                    best = z;
                    hit = sampler;
                }
            }

            return new
            {
                x = TopoUtils.Mm(point.X),
                y = TopoUtils.Mm(point.Y),
                elevationMm = best.HasValue ? TopoUtils.Mm(best.Value) : (double?)null,
                elementId = hit?.Element.Id.GetValue(),
                kind = hit == null ? null : TopoUtils.KindOf(hit.Element)
            };
        }).ToList();

        return new
        {
            found = results.Count(r => r.elevationMm.HasValue),
            outside = results.Count(r => !r.elevationMm.HasValue),
            points = results
        };
    }
}
