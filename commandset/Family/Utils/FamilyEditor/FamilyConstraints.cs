using Autodesk.Revit.DB;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

/// <summary>Описание размеров и выравниваний семейства.</summary>
public static class FamilyConstraints
{
    public static List<object> DescribeAll(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(Dimension))
            .Cast<Dimension>()
            .Select(d => Describe(doc, d))
            .ToList();
    }

    public static object Describe(Document doc, Dimension dimension)
    {
        var references = new List<object>();
        try
        {
            foreach (Reference reference in dimension.References)
            {
                var element = doc.GetElement(reference.ElementId);
                references.Add(new
                {
                    elementId = reference.ElementId.GetValue(),
                    kind = element?.GetType().Name,
                    name = element?.Name
                });
            }
        }
        catch (Exception)
        {
        }

        var segments = Try(() => dimension.NumberOfSegments, 0);
        return new
        {
            id = dimension.Id.GetValue(),
            // Выравнивание (NewAlignment) хранится как размер без значения.
            kind = Try(() => dimension.Value, null) == null && segments == 0 ? "alignment" : "dimension",
            valueMm = Try(() => dimension.Value, null) is double value ? FamilyDocInfo.Mm(value) : (double?)null,
            segmentsMm = segments > 1
                ? Try(() => dimension.Segments.Cast<DimensionSegment>()
                    .Select(s => s.Value.HasValue ? FamilyDocInfo.Mm(s.Value.Value) : (double?)null)
                    .ToList(), null)
                : null,
            label = Try(() => dimension.FamilyLabel?.Definition.Name, null),
            isLocked = Try(() => dimension.IsLocked, false),
            areSegmentsEqual = Try(() => dimension.AreSegmentsEqual, false),
            view = Try(() => dimension.View?.Name, null),
            references
        };
    }

    private static T Try<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
