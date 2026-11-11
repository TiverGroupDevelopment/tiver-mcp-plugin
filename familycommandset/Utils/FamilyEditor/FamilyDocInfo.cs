using Autodesk.Revit.DB;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

/// <summary>Виды, опорные плоскости и краткая сводка документа семейства.</summary>
public static class FamilyDocInfo
{
    private const double ParallelTolerance = 1e-6;

    // Значения параметра ELEM_REFERENCE_NAME («Является опорной»).
    private static readonly Dictionary<int, string> ReferenceTypeNames = new Dictionary<int, string>
    {
        [0] = "Left",
        [1] = "CenterLeftRight",
        [2] = "Right",
        [3] = "Front",
        [4] = "CenterFrontBack",
        [5] = "Back",
        [6] = "Bottom",
        [7] = "CenterElevation",
        [8] = "Top",
        [12] = "NotAReference",
        [13] = "StrongReference",
        [14] = "WeakReference"
    };

    public static IEnumerable<string> ReferenceTypes => ReferenceTypeNames.Values;

    public static double Mm(double feet)
    {
        return Math.Round(feet * 304.8, 3);
    }

    public static double[] Mm(XYZ point)
    {
        return new[] { Mm(point.X), Mm(point.Y), Mm(point.Z) };
    }

    public static double[] Unit(XYZ vector)
    {
        return new[] { Math.Round(vector.X, 6), Math.Round(vector.Y, 6), Math.Round(vector.Z, 6) };
    }

    public static List<View> Views(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(View))
            .Cast<View>()
            .Where(v => !v.IsTemplate && IsModelView(v.ViewType))
            .OrderBy(v => ViewOrder(v.ViewType))
            .ThenBy(v => v.Name)
            .ToList();
    }

    public static View FindView(Document doc, string name)
    {
        var views = Views(doc);
        return views.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException(
                   $"Вид '{name}' не найден. Доступные виды: {string.Join(", ", views.Select(v => $"{v.Name} ({v.ViewType})"))}");
    }

    /// <summary>
    /// Вид, в котором плоскости с нормалью <paramref name="normal"/> видны как линии:
    /// направление взгляда перпендикулярно нормали. Нужен для опорных плоскостей, размеров и выравнивания.
    /// </summary>
    public static View PickViewForNormal(Document doc, XYZ normal, string viewName)
    {
        var direction = normal.Normalize();
        if (viewName != null)
        {
            var requested = FindView(doc, viewName);
            if (!SeesEdgeOn(requested, direction))
            {
                throw new ArgumentException(
                    $"В виде '{requested.Name}' плоскости с нормалью [{string.Join(", ", Unit(direction))}] не видны как линии. Не передавайте view, чтобы вид был выбран автоматически.");
            }

            return requested;
        }

        return Views(doc).FirstOrDefault(v => v.ViewType != ViewType.ThreeD && SeesEdgeOn(v, direction))
               ?? throw new InvalidOperationException(
                   $"В семействе нет плана или фасада, перпендикулярного нормали [{string.Join(", ", Unit(direction))}]");
    }

    public static object DescribeView(View view)
    {
        XYZ direction = null;
        try
        {
            direction = view.ViewDirection;
        }
        catch (Exception)
        {
        }

        return new
        {
            id = view.Id.GetValue(),
            name = view.Name,
            type = view.ViewType.ToString(),
            viewDirection = direction == null ? null : Unit(direction)
        };
    }

    public static object DescribeReferencePlane(ReferencePlane plane)
    {
        var geometry = plane.GetPlane();
        var isReference = plane.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME);
        var definesOrigin = plane.get_Parameter(BuiltInParameter.DATUM_PLANE_DEFINES_ORIGIN);
        string referenceType = null;
        if (isReference != null && isReference.HasValue)
        {
            var value = isReference.AsInteger();
            referenceType = ReferenceTypeNames.TryGetValue(value, out var known) ? known : value.ToString();
        }

        return new
        {
            id = plane.Id.GetValue(),
            name = plane.Name,
            originMm = Mm(geometry.Origin),
            normal = Unit(geometry.Normal),
            bubbleEndMm = Mm(plane.BubbleEnd),
            freeEndMm = Mm(plane.FreeEnd),
            isReference = referenceType,
            definesOrigin = definesOrigin != null && definesOrigin.HasValue && definesOrigin.AsInteger() == 1
        };
    }

    public static int ParseReferenceType(string value)
    {
        var normalized = value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("/", string.Empty);
        foreach (var pair in ReferenceTypeNames)
        {
            if (string.Equals(pair.Value, normalized, StringComparison.OrdinalIgnoreCase)) return pair.Key;
        }

        throw new ArgumentException($"Неизвестное значение isReference '{value}'. Допустимо: {string.Join(", ", ReferenceTypeNames.Values)}");
    }

    public static ReferencePlane FindReferencePlane(Document doc, string name)
    {
        var planes = new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>().ToList();
        return planes.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException(
                   $"Опорная плоскость '{name}' не найдена. Именованные плоскости: {string.Join(", ", planes.Select(r => r.Name).Where(n => !string.IsNullOrEmpty(n)))}");
    }

    public static object Summarize(Document doc)
    {
        var manager = doc.FamilyManager;
        return new
        {
            title = doc.Title,
            path = string.IsNullOrEmpty(doc.PathName) ? null : doc.PathName,
            category = doc.OwnerFamily?.FamilyCategory?.Name,
            types = manager.Types.Size,
            parameters = manager.Parameters.Size,
            forms = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements().OfType<GenericForm>().Count(),
            referencePlanes = new FilteredElementCollector(doc)
                .OfClass(typeof(ReferencePlane))
                .Cast<ReferencePlane>()
                .Select(DescribeReferencePlane)
                .ToList(),
            levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Select(l => new { id = l.Id.GetValue(), name = l.Name, elevationMm = Mm(l.Elevation) })
                .ToList(),
            views = Views(doc).Select(DescribeView).ToList()
        };
    }

    private static bool SeesEdgeOn(View view, XYZ normal)
    {
        try
        {
            return Math.Abs(view.ViewDirection.Normalize().DotProduct(normal)) < ParallelTolerance;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsModelView(ViewType type)
    {
        return type == ViewType.FloorPlan || type == ViewType.CeilingPlan || type == ViewType.Elevation
               || type == ViewType.Section || type == ViewType.ThreeD;
    }

    private static int ViewOrder(ViewType type)
    {
        switch (type)
        {
            case ViewType.FloorPlan: return 0;
            case ViewType.Elevation: return 1;
            case ViewType.Section: return 2;
            case ViewType.CeilingPlan: return 3;
            default: return 4;
        }
    }
}
