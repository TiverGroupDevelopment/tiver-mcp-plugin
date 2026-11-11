using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

public sealed class FamilyEditSession : IDisposable
{
    private readonly Document _projectDocument;
    private bool _disposed;

    private FamilyEditSession(Document familyDocument, Document projectDocument, string familyName, bool isPinned)
    {
        Document = familyDocument;
        _projectDocument = projectDocument;
        FamilyName = familyName;
        IsPinned = isPinned;
    }

    public Document Document { get; }

    public string FamilyName { get; }

    /// <summary>Документ принадлежит закреплённой сессии (new_family / open_family) и живёт до close_family.</summary>
    public bool IsPinned { get; }

    /// <summary>Временный документ из EditFamily: после правки загружается в проект и закрывается.</summary>
    public bool IsDetached => _projectDocument != null;

    public static FamilyEditSession Open(UIApplication app, string familyName)
    {
        var pinned = FamilySessionRegistry.Current;
        if (pinned != null)
        {
            if (familyName != null && !string.Equals(pinned.FamilyName, familyName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Открыта сессия семейства '{pinned.FamilyName}', а запрошено '{familyName}'. Вызовите close_family или не передавайте familyName.");
            }

            return new FamilyEditSession(pinned.Document, null, pinned.FamilyName, true);
        }

        var active = app.ActiveUIDocument?.Document
                     ?? throw new InvalidOperationException("В Revit нет активного документа. Создайте семейство через new_family или откройте через open_family.");

        if (active.IsFamilyDocument)
        {
            var title = FamilySessionRegistry.StripExtension(active.Title);
            if (familyName != null && !string.Equals(title, familyName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Активен документ семейства '{title}', а запрошено '{familyName}'. Активируйте проект или нужное семейство либо не передавайте familyName.");
            }

            return new FamilyEditSession(active, null, title, false);
        }

        if (familyName == null)
        {
            throw new ArgumentException("Активен документ проекта: укажите familyName семейства, загруженного в проект, либо откройте сессию через open_family / new_family");
        }

        var family = FindLoadedFamily(active, familyName);
        return new FamilyEditSession(active.EditFamily(family), active, family.Name, false);
    }

    public static Family FindLoadedFamily(Document project, string familyName)
    {
        var family = new FilteredElementCollector(project)
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

        return family;
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
}
