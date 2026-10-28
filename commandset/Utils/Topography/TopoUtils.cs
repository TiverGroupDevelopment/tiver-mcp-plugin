using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Utils.Topography;

public static class TopoUtils
{
    public const double MillimetersPerFoot = 304.8;
    private const double SquareMetersPerSquareFoot = 0.09290304;

    public static double Mm(double feet)
    {
        return Math.Round(feet * MillimetersPerFoot, 3);
    }

    public static double[] Mm(XYZ point)
    {
        return new[] { Mm(point.X), Mm(point.Y), Mm(point.Z) };
    }

    public static double Feet(double mm)
    {
        return mm / MillimetersPerFoot;
    }

    public static XYZ ParsePoint3(JToken point)
    {
        if (point is JArray array && array.Count >= 3)
        {
            return new XYZ(Feet(array[0].Value<double>()), Feet(array[1].Value<double>()), Feet(array[2].Value<double>()));
        }

        if (point is JObject obj && obj["x"] != null && obj["y"] != null && obj["z"] != null)
        {
            return new XYZ(Feet(obj["x"].Value<double>()), Feet(obj["y"].Value<double>()), Feet(obj["z"].Value<double>()));
        }

        throw new ArgumentException($"Точка должна быть {{\"x\":..,\"y\":..,\"z\":..}} или [x, y, z] в мм, получено: {point}");
    }

    public static XYZ ParsePoint2(JToken point)
    {
        if (point is JArray array && array.Count >= 2)
        {
            return new XYZ(Feet(array[0].Value<double>()), Feet(array[1].Value<double>()), 0);
        }

        if (point is JObject obj && obj["x"] != null && obj["y"] != null)
        {
            return new XYZ(Feet(obj["x"].Value<double>()), Feet(obj["y"].Value<double>()), 0);
        }

        throw new ArgumentException($"Точка должна быть {{\"x\":..,\"y\":..}} или [x, y] в мм, получено: {point}");
    }

    public static List<XYZ> ParsePoints3(JObject args, string key)
    {
        return JsonArgs.RequiredArray(args, key).Select(ParsePoint3).ToList();
    }

    public static List<XYZ> ParsePoints2(JObject args, string key)
    {
        return JsonArgs.RequiredArray(args, key).Select(ParsePoint2).ToList();
    }

    public static IList<CurveLoop> BuildLoops(Document doc, JToken loops, double elevation)
    {
        if (!(loops is JArray array) || array.Count == 0)
        {
            throw new ArgumentException("boundary должен быть непустым массивом контуров");
        }

        return array.Select(loop => BuildLoop(doc, loop, elevation)).ToList();
    }

    public static CurveLoop BuildLoop(Document doc, JToken loop, double elevation)
    {
        var tolerance = doc.Application.ShortCurveTolerance;

        if (loop is JArray bare)
        {
            return Polyline(bare.Select(p => At(ParsePoint2(p), elevation)).ToList(), tolerance);
        }

        if (!(loop is JObject obj))
        {
            throw new ArgumentException($"Некорректный контур: {loop}");
        }

        if (obj["points"] is JArray points)
        {
            return Polyline(points.Select(p => At(ParsePoint2(p), elevation)).ToList(), tolerance);
        }

        if (obj["rectangle"] is JObject rectangle)
        {
            var min = ParsePoint2(rectangle["min"] ?? throw new ArgumentException("rectangle.min не задан"));
            var max = ParsePoint2(rectangle["max"] ?? throw new ArgumentException("rectangle.max не задан"));
            return Polyline(new List<XYZ>
            {
                new XYZ(min.X, min.Y, elevation),
                new XYZ(max.X, min.Y, elevation),
                new XYZ(max.X, max.Y, elevation),
                new XYZ(min.X, max.Y, elevation)
            }, tolerance);
        }

        if (obj["circle"] is JObject circle)
        {
            var center = At(ParsePoint2(circle["center"] ?? throw new ArgumentException("circle.center не задан")), elevation);
            var radius = Feet(circle["radius"]?.Value<double>() ?? throw new ArgumentException("circle.radius не задан"));
            if (radius <= tolerance) throw new ArgumentException("Радиус окружности слишком мал");
            var east = center + XYZ.BasisX * radius;
            var west = center - XYZ.BasisX * radius;
            var result = new CurveLoop();
            result.Append(Arc.Create(east, west, center + XYZ.BasisY * radius));
            result.Append(Arc.Create(west, east, center - XYZ.BasisY * radius));
            return result;
        }

        throw new ArgumentException("Контур должен быть массивом точек, {points}, {rectangle} или {circle}");
    }

    private static XYZ At(XYZ point, double elevation)
    {
        return new XYZ(point.X, point.Y, elevation);
    }

    private static CurveLoop Polyline(List<XYZ> points, double tolerance)
    {
        var cleaned = new List<XYZ>();
        foreach (var point in points)
        {
            if (cleaned.Count == 0 || cleaned[cleaned.Count - 1].DistanceTo(point) > tolerance) cleaned.Add(point);
        }

        if (cleaned.Count > 1 && cleaned[0].DistanceTo(cleaned[cleaned.Count - 1]) <= tolerance)
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        if (cleaned.Count < 3)
        {
            throw new ArgumentException("Контур должен содержать минимум 3 различные точки");
        }

        var loop = new CurveLoop();
        for (var i = 0; i < cleaned.Count; i++)
        {
            loop.Append(Line.CreateBound(cleaned[i], cleaned[(i + 1) % cleaned.Count]));
        }

        return loop;
    }

    /// <summary>
    /// Основные элементы рельефа: топоповерхности (без подобластей) и, начиная с 2024, топотела (без подразделений).
    /// </summary>
    public static List<Element> CollectMain(Document doc)
    {
        return Collect(doc).Where(e => !IsChild(e)).ToList();
    }

    public static List<Element> Collect(Document doc)
    {
        var result = new FilteredElementCollector(doc)
            .OfClass(typeof(TopographySurface))
            .ToElements()
            .ToList();
#if REVIT2024_OR_GREATER
        result.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Toposolid)).ToElements());
#endif
        return result;
    }

    public static Element Resolve(Document doc, JObject args, string key = "elementId")
    {
        if (JsonArgs.Has(args, key))
        {
            var id = FamilyEditorIds.FromNumber(args[key].Value<long>());
            var element = doc.GetElement(id) ?? throw new ArgumentException($"Элемент {id.GetValue()} не найден");
            if (KindOf(element) == null)
            {
                throw new ArgumentException($"Элемент {id.GetValue()} ({element.GetType().Name}) не является топоповерхностью или топотелом");
            }

            return element;
        }

        var candidates = CollectMain(doc);
        switch (candidates.Count)
        {
            case 0:
                throw new InvalidOperationException("В проекте нет топоповерхностей. Создайте её командой create_topography");
            case 1:
                return candidates[0];
            default:
                throw new ArgumentException(
                    $"В проекте {candidates.Count} элементов рельефа, укажите {key}: " +
                    string.Join(", ", candidates.Select(c => c.Id.GetValue())));
        }
    }

    public static string KindOf(Element element)
    {
        switch (element)
        {
            case TopographySurface surface:
                return surface.IsSiteSubRegion ? "siteSubRegion" : "topographySurface";
#if REVIT2024_OR_GREATER
            case Toposolid solid:
                return solid.HostTopoId != ElementId.InvalidElementId ? "toposolidSubdivision" : "toposolid";
#endif
            default:
                return null;
        }
    }

    public static bool IsChild(Element element)
    {
        var kind = KindOf(element);
        return kind == "siteSubRegion" || kind == "toposolidSubdivision";
    }

    public static Level ResolveLevel(Document doc, string name, double referenceElevation)
    {
        var levels = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(l => l.Elevation)
            .ToList();
        if (levels.Count == 0) throw new InvalidOperationException("В проекте нет уровней");

        if (name != null)
        {
            return levels.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
                   ?? throw new ArgumentException($"Уровень '{name}' не найден. Доступны: {string.Join(", ", levels.Select(l => l.Name))}");
        }

        return levels.LastOrDefault(l => l.Elevation <= referenceElevation + 1e-6) ?? levels[0];
    }

#if REVIT2024_OR_GREATER
    public static ToposolidType ResolveToposolidType(Document doc, string name)
    {
        var types = new FilteredElementCollector(doc).OfClass(typeof(ToposolidType)).Cast<ToposolidType>().ToList();
        if (types.Count == 0) throw new InvalidOperationException("В проекте нет типов топотел");
        if (name == null) return types[0];

        return types.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"Тип топотела '{name}' не найден. Доступны: {string.Join(", ", types.Select(t => t.Name))}");
    }
#endif

    public static ElementId ResolveMaterial(Document doc, string name)
    {
        if (name == null) return null;
        return FamilyEditorIds.FindMaterial(doc, name).Id;
    }

    public static string MaterialName(Document doc, Element element)
    {
        ElementId id = null;
        if (element is TopographySurface surface)
        {
            id = surface.MaterialId;
        }
        else
        {
            var type = doc.GetElement(element.GetTypeId());
            var structure = (type as HostObjAttributes)?.GetCompoundStructure();
            if (structure != null && structure.LayerCount > 0)
            {
                id = structure.GetMaterialId(structure.GetFirstCoreLayerIndex());
            }
        }

        return id == null || id == ElementId.InvalidElementId ? null : doc.GetElement(id)?.Name;
    }

    public static double? AreaM2(Element element, BuiltInParameter parameter)
    {
        var value = element.get_Parameter(parameter);
        if (value == null || !value.HasValue || value.StorageType != StorageType.Double) return null;
        return Math.Round(value.AsDouble() * SquareMetersPerSquareFoot, 3);
    }

    /// <summary>
    /// Ищет точку из набора, ближайшую в плане к заданной. null — если ближе tolerance нет.
    /// </summary>
    public static XYZ NearestInPlan(IEnumerable<XYZ> points, XYZ target, double tolerance)
    {
        XYZ best = null;
        var bestDistance = double.MaxValue;
        foreach (var point in points)
        {
            var dx = point.X - target.X;
            var dy = point.Y - target.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = point;
            }
        }

        return bestDistance <= tolerance ? best : null;
    }
}
