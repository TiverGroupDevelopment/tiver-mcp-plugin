using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.Topography;

/// <summary>
/// Отметка верха рельефа в точке плана: треугольники сетки топоповерхности или триангулированных граней топотела,
/// из всех треугольников, накрывающих точку, берётся самый верхний.
/// </summary>
public sealed class TopoSurfaceSampler
{
    private readonly List<XYZ[]> _triangles = new List<XYZ[]>();

    public TopoSurfaceSampler(Element element)
    {
        Element = element;
        var options = new Options { ComputeReferences = false, DetailLevel = ViewDetailLevel.Fine };
        Collect(element.get_Geometry(options), Transform.Identity);
        if (_triangles.Count == 0)
        {
            throw new InvalidOperationException($"У элемента {element.Id.GetValue()} нет геометрии поверхности");
        }

        MinX = _triangles.Min(t => t.Min(p => p.X));
        MinY = _triangles.Min(t => t.Min(p => p.Y));
        MaxX = _triangles.Max(t => t.Max(p => p.X));
        MaxY = _triangles.Max(t => t.Max(p => p.Y));
    }

    public Element Element { get; }
    public double MinX { get; }
    public double MinY { get; }
    public double MaxX { get; }
    public double MaxY { get; }

    public double? ElevationAt(double x, double y)
    {
        if (x < MinX - 1e-9 || x > MaxX + 1e-9 || y < MinY - 1e-9 || y > MaxY + 1e-9) return null;

        double? best = null;
        foreach (var t in _triangles)
        {
            var z = Interpolate(t[0], t[1], t[2], x, y);
            if (z.HasValue && (!best.HasValue || z.Value > best.Value)) best = z;
        }

        return best;
    }

    private void Collect(GeometryElement geometry, Transform transform)
    {
        if (geometry == null) return;

        foreach (var obj in geometry)
        {
            switch (obj)
            {
                case Mesh mesh:
                    AddMesh(mesh, transform);
                    break;
                case Solid solid when solid.Faces.Size > 0:
                    foreach (Face face in solid.Faces)
                    {
                        AddMesh(face.Triangulate(), transform);
                    }

                    break;
                case GeometryInstance instance:
                    Collect(instance.GetInstanceGeometry(), transform);
                    break;
            }
        }
    }

    private void AddMesh(Mesh mesh, Transform transform)
    {
        if (mesh == null) return;
        for (var i = 0; i < mesh.NumTriangles; i++)
        {
            var triangle = mesh.get_Triangle(i);
            var a = transform.OfPoint(triangle.get_Vertex(0));
            var b = transform.OfPoint(triangle.get_Vertex(1));
            var c = transform.OfPoint(triangle.get_Vertex(2));

            // Вертикальные треугольники (боковые грани топотела) не дают отметку
            var area2 = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (Math.Abs(area2) < 1e-12) continue;
            _triangles.Add(new[] { a, b, c });
        }
    }

    private static double? Interpolate(XYZ a, XYZ b, XYZ c, double x, double y)
    {
        var denominator = (b.Y - c.Y) * (a.X - c.X) + (c.X - b.X) * (a.Y - c.Y);
        var u = ((b.Y - c.Y) * (x - c.X) + (c.X - b.X) * (y - c.Y)) / denominator;
        var v = ((c.Y - a.Y) * (x - c.X) + (a.X - c.X) * (y - c.Y)) / denominator;
        var w = 1 - u - v;
        const double eps = -1e-9;
        if (u < eps || v < eps || w < eps) return null;
        return u * a.Z + v * b.Z + w * c.Z;
    }
}
