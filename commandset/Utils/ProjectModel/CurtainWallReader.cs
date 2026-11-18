using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.ProjectModel;

/// <summary>Чтение сетки витража: линии, импосты, панели в мм и в координатах вдоль стены.</summary>
public static class CurtainWallReader
{
    public sealed class MullionInfo
    {
        public long Id;
        public string Type;
        public string Orientation;
        public double LengthMm;
        public XYZ Start;
        public XYZ End;
    }

    public static (XYZ origin, XYZ direction, double length) Axis(Wall wall)
    {
        var curve = ((LocationCurve)wall.Location).Curve;
        var a = curve.GetEndPoint(0);
        var b = curve.GetEndPoint(1);
        var flat = new XYZ(b.X - a.X, b.Y - a.Y, 0);
        return (new XYZ(a.X, a.Y, 0), flat.Normalize(), flat.GetLength());
    }

    public static double Along(Wall wall, XYZ point)
    {
        var axis = Axis(wall);
        return (new XYZ(point.X, point.Y, 0) - axis.origin).DotProduct(axis.direction);
    }

    public static string Orientation(XYZ a, XYZ b)
    {
        var d = b - a;
        if (d.GetLength() < 1e-9) return "point";
        return Math.Abs(d.Normalize().Z) > 0.9 ? "vertical" : Math.Abs(d.Normalize().Z) < 0.1 ? "horizontal" : "sloped";
    }

    public static List<double> VerticalGridPositions(Wall wall)
    {
        var doc = wall.Document;
        var positions = new List<double>();
        var grid = wall.CurtainGrid;
        if (grid == null) return positions;
        foreach (var id in grid.GetVGridLineIds().Concat(grid.GetUGridLineIds()))
        {
            if (!(doc.GetElement(id) is CurtainGridLine line)) continue;
            var curve = line.FullCurve;
            if (Orientation(curve.GetEndPoint(0), curve.GetEndPoint(1)) == "vertical") positions.Add(Along(wall, curve.GetEndPoint(0)));
        }

        return positions.OrderBy(x => x).ToList();
    }

    public static List<double> HorizontalGridElevations(Wall wall)
    {
        var doc = wall.Document;
        var result = new List<double>();
        var grid = wall.CurtainGrid;
        if (grid == null) return result;
        foreach (var id in grid.GetVGridLineIds().Concat(grid.GetUGridLineIds()))
        {
            if (!(doc.GetElement(id) is CurtainGridLine line)) continue;
            var curve = line.FullCurve;
            if (Orientation(curve.GetEndPoint(0), curve.GetEndPoint(1)) == "horizontal") result.Add(curve.GetEndPoint(0).Z);
        }

        return result.OrderBy(z => z).ToList();
    }

    public static List<MullionInfo> Mullions(Wall wall)
    {
        var doc = wall.Document;
        var list = new List<MullionInfo>();
        var grid = wall.CurtainGrid;
        if (grid == null) return list;
        foreach (var id in grid.GetMullionIds())
        {
            if (!(doc.GetElement(id) is Mullion mullion)) continue;
            var curve = mullion.LocationCurve;
            if (curve == null) continue;
            var a = curve.GetEndPoint(0);
            var b = curve.GetEndPoint(1);
            list.Add(new MullionInfo
            {
                Id = id.GetValue(),
                Type = mullion.MullionType?.Name ?? doc.GetElement(mullion.GetTypeId())?.Name,
                Orientation = Orientation(a, b),
                LengthMm = ProjectModelUtils.Mm(curve.Length),
                Start = a,
                End = b
            });
        }

        return list;
    }

    public static object Describe(Document doc, Wall wall, bool details)
    {
        var grid = wall.CurtainGrid;
        var type = doc.GetElement(wall.GetTypeId()) as WallType;
        var axis = Axis(wall);
        var baseZ = ((LocationCurve)wall.Location).Curve.GetEndPoint(0).Z;
        if (grid == null)
        {
            return new { id = wall.Id.GetValue(), type = type?.Name, curtain = false };
        }

        var verticals = VerticalGridPositions(wall);
        var horizontals = HorizontalGridElevations(wall);
        var mullions = Mullions(wall);

        var panels = grid.GetPanelIds().Select(id => doc.GetElement(id)).Where(e => e != null).Select(e =>
        {
            var width = e.get_Parameter(BuiltInParameter.CURTAIN_WALL_PANELS_WIDTH)?.AsDouble() ?? 0;
            var height = e.get_Parameter(BuiltInParameter.CURTAIN_WALL_PANELS_HEIGHT)?.AsDouble() ?? 0;
            var panelType = doc.GetElement(e.GetTypeId()) as ElementType;
            return new { id = e.Id.GetValue(), type = panelType == null ? e.Name : ProjectModelUtils.FullName(panelType), widthMm = ProjectModelUtils.Mm(width), heightMm = ProjectModelUtils.Mm(height) };
        }).ToList();

        return new
        {
            id = wall.Id.GetValue(),
            type = type?.Name,
            curtain = true,
            lengthMm = ProjectModelUtils.Mm(axis.length),
            start = ProjectModelUtils.Mm(axis.origin),
            direction = new[] { Math.Round(axis.direction.X, 6), Math.Round(axis.direction.Y, 6) },
            baseElevationMm = ProjectModelUtils.Mm(baseZ),
            grid = new
            {
                verticalLinesAlongWallMm = verticals.Select(ProjectModelUtils.Mm).ToList(),
                horizontalLinesElevationMm = horizontals.Select(ProjectModelUtils.Mm).ToList(),
                verticalSpacingMm = Spacing(verticals),
                horizontalSpacingMm = Spacing(horizontals)
            },
            mullionsByType = mullions.GroupBy(m => new { m.Type, m.Orientation }).Select(g => new
            {
                type = g.Key.Type,
                orientation = g.Key.Orientation,
                count = g.Count(),
                minLengthMm = g.Min(m => m.LengthMm),
                maxLengthMm = g.Max(m => m.LengthMm),
                totalLengthMm = Math.Round(g.Sum(m => m.LengthMm), 1)
            }).ToList(),
            // Панели нулевого размера — признак того, что импосты легли вплотную (высота не кратна шагу, граничные импосты).
            zeroSizePanels = panels.Count(p => p.widthMm < 0.5 || p.heightMm < 0.5),
            panelsByType = panels.GroupBy(p => new { p.type, p.widthMm, p.heightMm }).Select(g => new
            {
                g.Key.type,
                g.Key.widthMm,
                g.Key.heightMm,
                count = g.Count()
            }).OrderBy(g => g.type).ThenBy(g => g.widthMm).ThenBy(g => g.heightMm).ToList(),
            mullions = details
                ? mullions.Select(m => new { id = m.Id, type = m.Type, orientation = m.Orientation, lengthMm = m.LengthMm, start = ProjectModelUtils.Mm(m.Start), end = ProjectModelUtils.Mm(m.End) }).ToList()
                : null,
            panels = details ? panels : null
        };
    }

    private static List<double> Spacing(List<double> values)
    {
        var result = new List<double>();
        for (var i = 1; i < values.Count; i++) result.Add(ProjectModelUtils.Mm(values[i] - values[i - 1]));
        return result.Distinct().ToList();
    }
}
