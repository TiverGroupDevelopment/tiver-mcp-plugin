using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;
using RevitMCPCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.Topography;

// API TopographySurface устарел в 2024, но нужен для Revit 2020–2023 и существующих моделей
#pragma warning disable CS0618

namespace RevitMCPCommandSet.Services.Topography;

public class EditTopographyPointsEventHandler : TopographyEventHandlerBase
{
    public override string GetName() => "Edit topography points";

    protected override object Run(Document doc, JObject args)
    {
        var element = TopoUtils.Resolve(doc, args);
        var delete = JsonArgs.Has(args, "delete") ? TopoUtils.ParsePoints2(args, "delete") : new List<XYZ>();
        var add = JsonArgs.Has(args, "add") ? TopoUtils.ParsePoints3(args, "add") : new List<XYZ>();
        var setElevation = JsonArgs.Has(args, "setElevation") ? TopoUtils.ParsePoints3(args, "setElevation") : new List<XYZ>();
        var tolerance = TopoUtils.Feet(JsonArgs.OptionalDouble(args, "toleranceMm", 50));

        if (delete.Count + add.Count + setElevation.Count == 0)
        {
            throw new ArgumentException("Передайте хотя бы один из массивов: add, delete, setElevation");
        }

        switch (element)
        {
            case TopographySurface surface:
                if (surface.IsSiteSubRegion || !surface.ArePointsEditable)
                {
                    throw new InvalidOperationException(
                        "Точки этой топоповерхности нельзя редактировать (подобласть или поверхность, связанная с площадкой здания)");
                }

                return EditSurfacePoints(doc, surface, "MCP: изменить точки топоповерхности",
                    () => EditSurface(surface, delete, add, setElevation, tolerance));
#if REVIT2024_OR_GREATER
            case Toposolid solid:
                return Edit(doc, "MCP: изменить точки топотела",
                    () => EditToposolid(doc, solid, delete, add, setElevation, tolerance));
#endif
            default:
                throw new InvalidOperationException($"Точки элемента вида '{TopoUtils.KindOf(element)}' не редактируются");
        }
    }

    private static object EditSurface(TopographySurface surface, List<XYZ> delete, List<XYZ> add, List<XYZ> setElevation, double tolerance)
    {
        if (delete.Count > 0)
        {
            var existing = surface.GetPoints();
            var matched = delete.Select(p => Require(TopoUtils.NearestInPlan(existing, p, tolerance), p)).Distinct().ToList();
            surface.DeletePoints(matched);
        }

        if (add.Count > 0)
        {
            surface.AddPoints(add);
        }

        foreach (var target in setElevation)
        {
            var point = Require(TopoUtils.NearestInPlan(surface.GetPoints(), target, tolerance), target);
            surface.ChangePointElevation(point, target.Z);
        }

        return Summary(surface.Id, delete, add, setElevation, surface.GetPoints().Count);
    }

#if REVIT2024_OR_GREATER
    private static object EditToposolid(Document doc, Toposolid solid, List<XYZ> delete, List<XYZ> add, List<XYZ> setElevation, double tolerance)
    {
        var shape = new ToposolidShape(doc, solid);

        foreach (var target in delete)
        {
            var vertex = shape.Find(target, tolerance) ?? throw NotFound(target);
            if (!shape.Delete(vertex)) throw new InvalidOperationException($"Revit не удалил точку {Describe(target)}");
            doc.Regenerate();
        }

        if (add.Count > 0)
        {
            var sampler = new TopoSurfaceSampler(solid);
            foreach (var point in add)
            {
                var top = sampler.ElevationAt(point.X, point.Y)
                          ?? throw new ArgumentException($"Точка {Describe(point)} лежит вне контура топотела");
                shape.Add(point, top);
                shape.SetElevation(point, point.Z, 1e-4);
            }
        }

        foreach (var target in setElevation)
        {
            shape.SetElevation(target, target.Z, tolerance);
        }

        return Summary(solid.Id, delete, add, setElevation, shape.Vertices().Count);
    }
#endif

    private static object Summary(ElementId id, List<XYZ> delete, List<XYZ> add, List<XYZ> setElevation, int pointCount)
    {
        return new
        {
            id = id.GetValue(),
            deleted = delete.Count,
            added = add.Count,
            elevationsChanged = setElevation.Count,
            pointCount
        };
    }

    private static XYZ Require(XYZ found, XYZ target)
    {
        return found ?? throw NotFound(target);
    }

    private static ArgumentException NotFound(XYZ target)
    {
        return new ArgumentException($"Рядом с {Describe(target)} нет точки рельефа. Увеличьте toleranceMm или уточните координаты (get_topography с includePoints)");
    }

    private static string Describe(XYZ point)
    {
        return $"({TopoUtils.Mm(point.X)}, {TopoUtils.Mm(point.Y)})";
    }
}
