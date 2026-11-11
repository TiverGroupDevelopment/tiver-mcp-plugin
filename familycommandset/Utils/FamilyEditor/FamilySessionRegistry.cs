using Autodesk.Revit.DB;
using System.IO;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

public sealed class FamilyCheckpoint
{
    public FamilyCheckpoint(int id, string label, string path)
    {
        Id = id;
        Label = label;
        Path = path;
        CreatedAt = DateTime.Now;
    }

    public int Id { get; }
    public string Label { get; }
    public string Path { get; }
    public DateTime CreatedAt { get; }

    public object Describe()
    {
        return new { id = Id, label = Label, createdAt = CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"), path = Path };
    }
}

/// <summary>
/// Закреплённая сессия редактирования семейства: документ открыт один раз (new_family / open_family)
/// и остаётся целью всех команд семейств до close_family. Без неё каждая команда из проекта делает
/// EditFamily → правка → LoadFamily → закрытие.
/// </summary>
public sealed class PinnedFamilySession
{
    private readonly List<FamilyCheckpoint> _checkpoints = new List<FamilyCheckpoint>();
    private readonly string _checkpointRoot =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "revit-mcp", "checkpoints", Guid.NewGuid().ToString("N"));

    public PinnedFamilySession(Document document, Document projectDocument, string origin, string projectFamilyName)
    {
        Document = document;
        ProjectDocument = projectDocument;
        Origin = origin;
        ProjectFamilyName = projectFamilyName;
        OriginalPath = string.IsNullOrEmpty(document.PathName) ? null : document.PathName;
    }

    public Document Document { get; private set; }

    /// <summary>Проект, из которого семейство открыто через EditFamily; null для new/file/active.</summary>
    public Document ProjectDocument { get; }

    /// <summary>new | file | project | active</summary>
    public string Origin { get; }

    /// <summary>Файл, в который семейство должно сохраняться; контрольные точки уводят документ во временную папку.</summary>
    public string OriginalPath { get; set; }

    private string ProjectFamilyName { get; }

    public string FamilyName => ProjectFamilyName ?? FamilySessionRegistry.StripExtension(Document.Title);

    public IReadOnlyList<FamilyCheckpoint> Checkpoints => _checkpoints;

    /// <summary>Документ сейчас лежит в папке контрольных точек, а не в своём файле.</summary>
    public bool IsRelocated =>
        !string.IsNullOrEmpty(Document.PathName)
        && Document.PathName.StartsWith(_checkpointRoot, StringComparison.OrdinalIgnoreCase);

    public FamilyCheckpoint AddCheckpoint(string label)
    {
        var id = _checkpoints.Count == 0 ? 1 : _checkpoints[_checkpoints.Count - 1].Id + 1;
        var directory = System.IO.Path.Combine(_checkpointRoot, id.ToString());
        Directory.CreateDirectory(directory);

        // Имя файла равно имени семейства: SaveAs меняет заголовок документа, а имя семейства должно сохраниться.
        var fileName = string.Concat(FamilyName.Split(System.IO.Path.GetInvalidFileNameChars())) + ".rfa";
        var path = System.IO.Path.Combine(directory, fileName);
        Document.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = true });

        var checkpoint = new FamilyCheckpoint(id, label, path);
        _checkpoints.Add(checkpoint);
        return checkpoint;
    }

    public void Rebind(Document document)
    {
        Document = document;
    }

    public void DeleteCheckpointFiles()
    {
        try
        {
            if (Directory.Exists(_checkpointRoot)) Directory.Delete(_checkpointRoot, true);
        }
        catch (Exception)
        {
        }
    }

    public object Describe()
    {
        return new
        {
            family = FamilyName,
            origin = Origin,
            path = IsRelocated ? OriginalPath : (string.IsNullOrEmpty(Document.PathName) ? null : Document.PathName),
            isModified = Document.IsModified || IsRelocated,
            project = ProjectDocument != null && ProjectDocument.IsValidObject ? ProjectDocument.Title : null,
            checkpoints = _checkpoints.Select(c => c.Describe()).ToList()
        };
    }
}

public static class FamilySessionRegistry
{
    private static PinnedFamilySession _current;

    /// <summary>Текущая закреплённая сессия или null. Сессия с закрытым вручную документом сбрасывается.</summary>
    public static PinnedFamilySession Current
    {
        get
        {
            if (_current != null && !_current.Document.IsValidObject) _current = null;
            return _current;
        }
    }

    public static PinnedFamilySession Pin(Document document, Document projectDocument, string origin, string projectFamilyName = null)
    {
        var existing = Current;
        if (existing != null)
        {
            throw new InvalidOperationException(
                $"Уже открыта сессия семейства '{existing.FamilyName}' ({existing.Origin}). Сначала вызовите close_family.");
        }

        if (!document.IsFamilyDocument)
        {
            throw new InvalidOperationException($"Документ '{document.Title}' не является семейством");
        }

        _current = new PinnedFamilySession(document, projectDocument, origin, projectFamilyName);
        return _current;
    }

    public static void Unpin()
    {
        _current = null;
    }

    public static string StripExtension(string title)
    {
        return title != null && title.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase)
            ? title.Substring(0, title.Length - 4)
            : title;
    }
}

public sealed class FamilyLoadOptions : IFamilyLoadOptions
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
