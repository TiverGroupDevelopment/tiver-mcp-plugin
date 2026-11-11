using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class CloseFamilyEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Close family";

    protected override object Run(UIApplication app, JObject args)
    {
        var session = FamilySessionRegistry.Current
                      ?? throw new InvalidOperationException("Нет открытой сессии семейства (new_family / open_family)");
        var doc = session.Document;
        var familyName = session.FamilyName;
        var relocated = session.IsRelocated;

        var savePath = JsonArgs.OptionalString(args, "savePath");
        var save = JsonArgs.OptionalBool(args, "save", false) || savePath != null;
        var loadIntoProject = JsonArgs.Has(args, "loadIntoProject")
            ? JsonArgs.OptionalBool(args, "loadIntoProject", false)
            : session.Origin == "project";
        var keepOpen = JsonArgs.OptionalBool(args, "keepOpen", false);
        var discardChanges = JsonArgs.OptionalBool(args, "discardChanges", false);

        // Все проверки — до первого необратимого действия.
        // После контрольной точки IsModified сброшен, хотя в исходный файл изменения не попали.
        if (!save && !loadIntoProject && !keepOpen && (doc.IsModified || relocated) && !discardChanges)
        {
            throw new InvalidOperationException(
                $"В семействе '{familyName}' есть несохранённые изменения. Передайте save: true / savePath, loadIntoProject: true или discardChanges: true.");
        }

        string saveAsPath = null;
        var overwrite = false;
        if (savePath != null)
        {
            saveAsPath = FamilyFiles.ValidateSavePath(savePath, JsonArgs.OptionalBool(args, "overwrite", false), out overwrite);
        }
        else if (save && relocated)
        {
            saveAsPath = session.OriginalPath
                         ?? throw new ArgumentException("Семейство ещё не сохранялось в свой файл: укажите savePath (*.rfa)");
            overwrite = true;
        }
        else if (save && string.IsNullOrEmpty(doc.PathName))
        {
            throw new ArgumentException("Семейство ещё не сохранялось на диск: укажите savePath (*.rfa)");
        }

        Document project = null;
        if (loadIntoProject)
        {
            project = session.ProjectDocument != null && session.ProjectDocument.IsValidObject
                ? session.ProjectDocument
                : app.ActiveUIDocument?.Document;
            if (project == null || project.IsFamilyDocument)
            {
                throw new InvalidOperationException("Нет проекта для загрузки семейства: активируйте документ проекта или передайте loadIntoProject: false");
            }
        }

        string savedTo = null;
        if (saveAsPath != null)
        {
            doc.SaveAs(saveAsPath, new SaveAsOptions { OverwriteExistingFile = overwrite });
            session.OriginalPath = saveAsPath;
            savedTo = saveAsPath;
            familyName = session.FamilyName;
        }
        else if (save)
        {
            doc.Save();
            savedTo = doc.PathName;
        }

        object loaded = null;
        if (project != null)
        {
            var family = doc.LoadFamily(project, new FamilyLoadOptions(JsonArgs.OptionalBool(args, "overwriteProjectValues", true)));
            if (HasFailureErrors)
            {
                throw new InvalidOperationException($"Семейство '{familyName}' не загружено в проект '{project.Title}'{FailureErrors}");
            }

            loaded = new { project = project.Title, family = family?.Name ?? familyName };
        }

        var closed = false;
        string note = null;
        if (!keepOpen)
        {
            var activeDocument = app.ActiveUIDocument?.Document;
            if (activeDocument != null && activeDocument.Equals(doc))
            {
                note = "Документ семейства активен в интерфейсе Revit: API не закрывает активный документ, он оставлен открытым";
            }
            else
            {
                closed = doc.Close(false);
            }
        }

        if (closed)
        {
            session.DeleteCheckpointFiles();
        }
        else if (session.IsRelocated)
        {
            note = (note == null ? string.Empty : note + ". ")
                   + $"Документ остался открытым из временной папки контрольных точек: {doc.PathName}";
        }

        FamilySessionRegistry.Unpin();

        return new
        {
            family = familyName,
            origin = session.Origin,
            savedTo,
            loadedIntoProject = loaded,
            closed,
            note,
            warnings = Warnings
        };
    }
}
