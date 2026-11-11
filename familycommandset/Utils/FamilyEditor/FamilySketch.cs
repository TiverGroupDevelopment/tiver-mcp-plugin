using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

public sealed class FamilyWorkPlane
{
    private const double MillimetersPerFoot = 304.8;

    private FamilyWorkPlane(SketchPlane sketchPlane, string description)
    {
        SketchPlane = sketchPlane;
        var plane = sketchPlane.GetPlane();
        Origin = plane.Origin;
        XAxis = plane.XVec.Normalize();
        YAxis = plane.YVec.Normalize();
        Normal = plane.Normal.Normalize();
        Description = description;
    }

    private FamilyWorkPlane(string description)
    {
        Origin = XYZ.Zero;
        XAxis = XYZ.BasisX;
        YAxis = XYZ.BasisY;
        Normal = XYZ.BasisZ;
        Description = description;
    }

    /// <summary>
    /// Локальная система профиля sweep / swept blend: контур задаётся в плоскости XY вокруг начала координат,
    /// Revit сам переносит его на траекторию. Эскизной плоскости у такого профиля нет.
    /// </summary>
    public static FamilyWorkPlane ProfileLocal()
    {
        return new FamilyWorkPlane("profile-local");
    }

    public SketchPlane SketchPlane { get; }
    public XYZ Origin { get; }
    public XYZ XAxis { get; }
    public XYZ YAxis { get; }
    public XYZ Normal { get; }
    public string Description { get; }

    public static FamilyWorkPlane Create(Document doc, JToken spec, string defaultPlane)
    {
        var settings = spec as JObject ?? new JObject();
        var offset = (settings["offset"]?.Value<double?>() ?? 0.0) / MillimetersPerFoot;
        var referencePlaneName = settings["referencePlane"]?.Value<string>();

        if (!string.IsNullOrWhiteSpace(referencePlaneName))
        {
            var referencePlane = new FilteredElementCollector(doc)
                                     .OfClass(typeof(ReferencePlane))
                                     .Cast<ReferencePlane>()
                                     .FirstOrDefault(r => string.Equals(r.Name, referencePlaneName, StringComparison.OrdinalIgnoreCase))
                                 ?? throw new ArgumentException($"Опорная плоскость '{referencePlaneName}' не найдена");

            if (Math.Abs(offset) < 1e-9)
            {
                return new FamilyWorkPlane(SketchPlane.Create(doc, referencePlane.Id), $"referencePlane:{referencePlane.Name}");
            }

            var basis = referencePlane.GetPlane();
            var shifted = Plane.CreateByOriginAndBasis(basis.Origin + basis.Normal.Normalize() * offset, basis.XVec, basis.YVec);
            return new FamilyWorkPlane(SketchPlane.Create(doc, shifted), $"referencePlane:{referencePlane.Name}+offset");
        }

        var name = (settings["plane"]?.Value<string>() ?? defaultPlane).Trim().ToUpperInvariant();
        Plane plane;
        switch (name)
        {
            case "XY":
                plane = Plane.CreateByOriginAndBasis(XYZ.BasisZ * offset, XYZ.BasisX, XYZ.BasisY);
                break;
            case "XZ":
                plane = Plane.CreateByOriginAndBasis(XYZ.BasisY.Negate() * offset, XYZ.BasisX, XYZ.BasisZ);
                break;
            case "YZ":
                plane = Plane.CreateByOriginAndBasis(XYZ.BasisX * offset, XYZ.BasisY, XYZ.BasisZ);
                break;
            default:
                throw new ArgumentException($"Неизвестная плоскость '{name}'. Допустимо: XY, XZ, YZ или referencePlane");
        }

        return new FamilyWorkPlane(SketchPlane.Create(doc, plane), name);
    }

    public XYZ ToWorld(JToken point, double normalOffsetMm = 0)
    {
        if (point is JArray pair && pair.Count >= 2)
        {
            return ToWorld(pair[0].Value<double>(), pair[1].Value<double>(), normalOffsetMm);
        }

        if (point is JObject obj && obj["x"] != null && obj["y"] != null)
        {
            return ToWorld(obj["x"].Value<double>(), obj["y"].Value<double>(), normalOffsetMm);
        }

        throw new ArgumentException($"Точка должна быть {{\"x\":..,\"y\":..}} или [x, y], получено: {point}");
    }

    public XYZ ToWorld(double xMm, double yMm, double normalOffsetMm = 0)
    {
        return Origin
               + XAxis * (xMm / MillimetersPerFoot)
               + YAxis * (yMm / MillimetersPerFoot)
               + Normal * (normalOffsetMm / MillimetersPerFoot);
    }

    public object Describe()
    {
        return new
        {
            source = Description,
            sketchPlaneId = SketchPlane?.Id.GetValue(),
            normal = new[] { Math.Round(Normal.X, 6), Math.Round(Normal.Y, 6), Math.Round(Normal.Z, 6) }
        };
    }
}

public static class FamilyProfileBuilder
{
    public static CurveArrArray BuildProfile(Document doc, FamilyWorkPlane plane, JToken loops, double normalOffsetMm = 0)
    {
        if (!(loops is JArray array) || array.Count == 0)
        {
            throw new ArgumentException("profile должен быть непустым массивом контуров");
        }

        var profile = new CurveArrArray();
        foreach (var loop in array)
        {
            profile.Append(BuildLoop(doc, plane, loop, normalOffsetMm));
        }

        return profile;
    }

    public static CurveArray BuildLoop(Document doc, FamilyWorkPlane plane, JToken loop, double normalOffsetMm = 0)
    {
        var tolerance = doc.Application.ShortCurveTolerance;
        var curves = new CurveArray();

        if (loop is JArray bare)
        {
            AppendPolyline(curves, bare.Select(p => plane.ToWorld(p, normalOffsetMm)).ToList(), tolerance);
            return curves;
        }

        if (!(loop is JObject obj))
        {
            throw new ArgumentException($"Некорректный контур: {loop}");
        }

        if (obj["points"] is JArray points)
        {
            AppendPolyline(curves, points.Select(p => plane.ToWorld(p, normalOffsetMm)).ToList(), tolerance);
        }
        else if (obj["rectangle"] is JObject rectangle)
        {
            var min = rectangle["min"] ?? throw new ArgumentException("rectangle.min не задан");
            var max = rectangle["max"] ?? throw new ArgumentException("rectangle.max не задан");
            double x0 = Coordinate(min, "x", 0), y0 = Coordinate(min, "y", 1);
            double x1 = Coordinate(max, "x", 0), y1 = Coordinate(max, "y", 1);
            AppendPolyline(curves, new List<XYZ>
            {
                plane.ToWorld(x0, y0, normalOffsetMm),
                plane.ToWorld(x1, y0, normalOffsetMm),
                plane.ToWorld(x1, y1, normalOffsetMm),
                plane.ToWorld(x0, y1, normalOffsetMm)
            }, tolerance);
        }
        else if (obj["circle"] is JObject circle)
        {
            var center = plane.ToWorld(circle["center"] ?? new JObject { ["x"] = 0, ["y"] = 0 }, normalOffsetMm);
            var radius = (circle["radius"]?.Value<double>() ?? throw new ArgumentException("circle.radius не задан")) / 304.8;
            if (radius <= tolerance) throw new ArgumentException("Радиус окружности слишком мал");
            var east = center + plane.XAxis * radius;
            var west = center - plane.XAxis * radius;
            curves.Append(Arc.Create(east, west, center + plane.YAxis * radius));
            curves.Append(Arc.Create(west, east, center - plane.YAxis * radius));
        }
        else if (obj["segments"] is JArray segments)
        {
            AppendSegments(curves, plane, segments, normalOffsetMm, tolerance);
        }
        else
        {
            throw new ArgumentException("Контур должен содержать points, rectangle, circle или segments");
        }

        return curves;
    }

    /// <summary>
    /// Траектория sweep: незамкнутая или замкнутая цепочка кривых в рабочей плоскости.
    /// Форматы: массив точек, {points, closed}, {segments}, {circle}.
    /// </summary>
    public static CurveArray BuildPath(Document doc, FamilyWorkPlane plane, JToken path)
    {
        var tolerance = doc.Application.ShortCurveTolerance;
        var curves = new CurveArray();

        if (path is JArray bare)
        {
            AppendOpenPolyline(curves, bare.Select(p => plane.ToWorld(p)).ToList(), false, tolerance);
            return curves;
        }

        if (!(path is JObject obj))
        {
            throw new ArgumentException($"Некорректная траектория: {path}");
        }

        if (obj["points"] is JArray points)
        {
            var closed = obj["closed"]?.Value<bool?>() ?? false;
            AppendOpenPolyline(curves, points.Select(p => plane.ToWorld(p)).ToList(), closed, tolerance);
        }
        else if (obj["segments"] is JArray segments)
        {
            AppendSegments(curves, plane, segments, 0, tolerance, false);
        }
        else if (obj["circle"] is JObject)
        {
            return BuildLoop(doc, plane, obj);
        }
        else
        {
            throw new ArgumentException("Траектория должна содержать points, segments или circle");
        }

        return curves;
    }

    private static void AppendOpenPolyline(CurveArray curves, List<XYZ> points, bool closed, double tolerance)
    {
        var cleaned = new List<XYZ>();
        foreach (var point in points)
        {
            if (cleaned.Count == 0 || cleaned[cleaned.Count - 1].DistanceTo(point) > tolerance) cleaned.Add(point);
        }

        if (cleaned.Count < 2)
        {
            throw new ArgumentException("Траектория из точек должна содержать минимум 2 различные точки");
        }

        for (var i = 0; i < cleaned.Count - 1; i++)
        {
            curves.Append(Line.CreateBound(cleaned[i], cleaned[i + 1]));
        }

        if (closed && cleaned.Count > 2 && cleaned[0].DistanceTo(cleaned[cleaned.Count - 1]) > tolerance)
        {
            curves.Append(Line.CreateBound(cleaned[cleaned.Count - 1], cleaned[0]));
        }
    }

    private static double Coordinate(JToken point, string key, int index)
    {
        if (point is JArray array && array.Count > index) return array[index].Value<double>();
        return point[key]?.Value<double>() ?? throw new ArgumentException($"Нет координаты '{key}' в {point}");
    }

    private static void AppendPolyline(CurveArray curves, List<XYZ> points, double tolerance)
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
            throw new ArgumentException("Контур из точек должен содержать минимум 3 различные точки");
        }

        for (var i = 0; i < cleaned.Count; i++)
        {
            curves.Append(Line.CreateBound(cleaned[i], cleaned[(i + 1) % cleaned.Count]));
        }
    }

    private static void AppendSegments(CurveArray curves, FamilyWorkPlane plane, JArray segments, double normalOffsetMm, double tolerance, bool mustClose = true)
    {
        XYZ firstStart = null;
        XYZ previousEnd = null;
        foreach (var segment in segments)
        {
            var type = segment["type"]?.Value<string>()?.ToLowerInvariant() ?? "line";
            var start = plane.ToWorld(segment["start"] ?? throw new ArgumentException("У сегмента нет start"), normalOffsetMm);
            var end = plane.ToWorld(segment["end"] ?? throw new ArgumentException("У сегмента нет end"), normalOffsetMm);

            if (previousEnd != null && previousEnd.DistanceTo(start) > tolerance)
            {
                throw new ArgumentException("Сегменты контура не образуют непрерывную цепочку: start не совпадает с end предыдущего");
            }

            Curve curve;
            switch (type)
            {
                case "line":
                    curve = Line.CreateBound(start, end);
                    break;
                case "arc":
                    var mid = plane.ToWorld(segment["mid"] ?? throw new ArgumentException("У дуги нет mid (точка на дуге)"), normalOffsetMm);
                    curve = Arc.Create(start, end, mid);
                    break;
                default:
                    throw new ArgumentException($"Неизвестный тип сегмента '{type}'. Допустимо: line, arc");
            }

            curves.Append(curve);
            firstStart ??= start;
            previousEnd = end;
        }

        if (firstStart == null)
        {
            throw new ArgumentException("segments пуст");
        }

        if (mustClose && previousEnd.DistanceTo(firstStart) > tolerance)
        {
            throw new ArgumentException("Контур из сегментов не замкнут");
        }
    }
}
