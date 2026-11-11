using Autodesk.Revit.DB;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

public static class FamilyEditorIds
{
#if REVIT2024_OR_GREATER
    public static ElementId FromNumber(long value)
    {
        return new ElementId(value);
    }
#else
    public static ElementId FromNumber(long value)
    {
        return new ElementId(checked((int)value));
    }
#endif

    public static Material FindMaterial(Document doc, string name)
    {
        return new FilteredElementCollector(doc)
                   .OfClass(typeof(Material))
                   .Cast<Material>()
                   .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"Материал '{name}' не найден в документе семейства");
    }
}
