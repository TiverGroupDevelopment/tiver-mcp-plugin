using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

public sealed class FamilyEditSession : IDisposable
{
    private readonly Document _projectDocument;
    private bool _disposed;

    private FamilyEditSession(Document familyDocument, Document projectDocument, string familyName)
    {
        Document = familyDocument;
        _projectDocument = projectDocument;
        FamilyName = familyName;
    }

    public Document Document { get; }

    public string FamilyName { get; }

    public bool IsDetached => _projectDocument != null;

    public static FamilyEditSession Open(UIApplication app, string familyName)
    {
        var active = app.ActiveUIDocument?.Document
                     ?? throw new InvalidOperationException("В Revit нет активного документа");

        if (active.IsFamilyDocument)
        {
            var title = StripExtension(active.Title);
            if (familyName != null && !string.Equals(title, familyName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Активен документ семейства '{title}', а запрошено '{familyName}'. Активируйте проект или нужное семейство либо не передавайте familyName.");
            }

            return new FamilyEditSession(active, null, title);
        }

        if (familyName == null)
        {
            throw new ArgumentException("Активен документ проекта: укажите familyName семейства, загруженного в проект");
        }

        var family = new FilteredElementCollector(active)
                         .OfClass(typeof(Family))
                         .Cast<Family>()
                         .FirstOrDefault(f => string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase))
                     ?? throw new ArgumentException($"Семейство '{familyName}' не найдено в проекте");

        if (family.IsInPlace)
        {
            throw new InvalidOperationException($"'{family.Name}' — семейство «по месту», API не позволяет открыть его через EditFamily");
        }

        if (!family.IsEditable)
        {
            throw new InvalidOperationException($"Семейство '{family.Name}' нельзя редактировать");
        }

        return new FamilyEditSession(active.EditFamily(family), active, family.Name);
    }

    public void LoadIntoProject(bool overwriteParameterValues)
    {
        if (!IsDetached) return;
        Document.LoadFamily(_projectDocument, new FamilyLoadOptions(overwriteParameterValues));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!IsDetached) return;
        try
        {
            Document.Close(false);
        }
        catch (Exception)
        {
        }
    }

    private static string StripExtension(string title)
    {
        return title.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase) ? title.Substring(0, title.Length - 4) : title;
    }

    private sealed class FamilyLoadOptions : IFamilyLoadOptions
    {
        private readonly bool _overwriteParameterValues;

        public FamilyLoadOptions(bool overwriteParameterValues)
        {
            _overwriteParameterValues = overwriteParameterValues;
        }

        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = _overwriteParameterValues;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = _overwriteParameterValues;
            return true;
        }
    }
}
