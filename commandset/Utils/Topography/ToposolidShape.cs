#if REVIT2024_OR_GREATER
using Autodesk.Revit.DB;

namespace RevitMCPCommandSet.Utils.Topography;

/// <summary>
/// Редактирование точек топотела через SlabShapeEditor. Смещение вершины в ModifySubElement задаётся
/// относительно исходной плоскости верха, поэтому перед изменением отметок она калибруется.
/// </summary>
public sealed class ToposolidShape
{
    private readonly Document _doc;
    private readonly SlabShapeEditor _editor;
    private double? _baseline;

    public ToposolidShape(Document doc, Toposolid toposolid)
    {
        _doc = doc;
        _editor = toposolid.GetSlabShapeEditor() ?? throw new InvalidOperationException("Форма топотела недоступна для редактирования");
        if (!_editor.IsEnabled)
        {
            _editor.Enable();
            _doc.Regenerate();
        }
    }

    /// <summary>
    /// Чтение без транзакции: если форма ещё не редактировалась, точек нет.
    /// </summary>
    public static List<SlabShapeVertex> ReadVertices(Toposolid toposolid)
    {
        var editor = toposolid.GetSlabShapeEditor();
        return editor != null && editor.IsEnabled
            ? editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList()
            : new List<SlabShapeVertex>();
    }

    public List<SlabShapeVertex> Vertices()
    {
        return _editor.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
    }

    public SlabShapeVertex Find(XYZ planPoint, double tolerance)
    {
        var position = TopoUtils.NearestInPlan(Vertices().Select(v => v.Position), planPoint, tolerance);
        return position == null ? null : Vertices().First(v => v.Position.IsAlmostEqualTo(position));
    }

    public void Add(XYZ planPoint, double currentTopZ)
    {
        var onTop = new XYZ(planPoint.X, planPoint.Y, currentTopZ);
#if REVIT2025_OR_GREATER
        _editor.AddPoint(onTop);
#else
        if (_editor.DrawPoint(onTop) == null)
        {
            throw new InvalidOperationException($"Точка ({TopoUtils.Mm(planPoint.X)}, {TopoUtils.Mm(planPoint.Y)}) не лежит на верхней грани топотела");
        }
#endif
        _doc.Regenerate();
    }

    public bool Delete(SlabShapeVertex vertex)
    {
#if REVIT2025_OR_GREATER
        if (vertex.VertexType == SlabShapeVertexType.Corner)
        {
            throw new InvalidOperationException(
                $"Угловую точку ({TopoUtils.Mm(vertex.Position.X)}, {TopoUtils.Mm(vertex.Position.Y)}) топотела удалить нельзя — измените её отметку или контур");
        }

        return _editor.DeletePoint(vertex);
#else
        throw new NotSupportedException("Удаление точек топотела доступно в Revit 2025 и новее");
#endif
    }

    public void SetElevation(XYZ planPoint, double z, double tolerance)
    {
        var baseline = Baseline();
        var vertex = Find(planPoint, tolerance)
                     ?? throw new ArgumentException($"Точка ({TopoUtils.Mm(planPoint.X)}, {TopoUtils.Mm(planPoint.Y)}) не найдена на топотеле");
        _editor.ModifySubElement(vertex, z - baseline);
        _doc.Regenerate();
    }

    private double Baseline()
    {
        if (_baseline.HasValue) return _baseline.Value;

        var probe = Vertices().FirstOrDefault() ?? throw new InvalidOperationException("У топотела нет точек формы");
        var position = probe.Position;
        _editor.ModifySubElement(probe, 0);
        _doc.Regenerate();

        var reset = Find(position, 1e-6) ?? throw new InvalidOperationException("Не удалось откалибровать отметку топотела");
        var baseline = reset.Position.Z;
        _editor.ModifySubElement(reset, position.Z - baseline);
        _doc.Regenerate();

        _baseline = baseline;
        return baseline;
    }
}
#endif
