using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>Загружает текущее состояние семейства в проект, не закрывая сессию — чтобы проверить его в деле.</summary>
public class LoadFamilyIntoProjectEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Load family into project";

    protected override object Run(UIApplication app, JObject args)
    {
        var projectTitle = JsonArgs.OptionalString(args, "project");
        var overwriteValues = JsonArgs.OptionalBool(args, "overwriteProjectValues", true);

        var pinned = FamilySessionRegistry.Current;
        var active = app.ActiveUIDocument?.Document;
        var familyDoc = pinned?.Document
                        ?? (active != null && active.IsFamilyDocument ? active : null)
                        ?? throw new InvalidOperationException("Нет семейства для загрузки: откройте сессию (new_family / open_family) или активируйте документ семейства");

        var projects = app.Application.Documents.Cast<Document>().Where(d => !d.IsFamilyDocument && !d.IsLinked).ToList();
        Document project;
        if (projectTitle != null)
        {
            project = projects.FirstOrDefault(d => string.Equals(d.Title, projectTitle, StringComparison.OrdinalIgnoreCase)
                                                   || string.Equals(System.IO.Path.GetFileNameWithoutExtension(d.Title), projectTitle, StringComparison.OrdinalIgnoreCase))
                      ?? throw new ArgumentException($"Проект '{projectTitle}' не открыт. Открытые проекты: {string.Join(", ", projects.Select(d => d.Title))}");
        }
        else if (pinned?.ProjectDocument != null && pinned.ProjectDocument.IsValidObject)
        {
            project = pinned.ProjectDocument;
        }
        else if (active != null && !active.IsFamilyDocument)
        {
            project = active;
        }
        else if (projects.Count == 1)
        {
            project = projects[0];
        }
        else
        {
            throw new InvalidOperationException(projects.Count == 0
                ? "В Revit не открыт ни один проект"
                : $"Открыто несколько проектов, укажите project: {string.Join(", ", projects.Select(d => d.Title))}");
        }

        var family = familyDoc.LoadFamily(project, new FamilyLoadOptions(overwriteValues));
        if (HasFailureErrors)
        {
            throw new InvalidOperationException($"Семейство не загружено в проект '{project.Title}'{FailureErrors}");
        }

        return new
        {
            project = project.Title,
            family = family?.Name,
            types = family == null
                ? null
                : family.GetFamilySymbolIds()
                    .Select(id => project.GetElement(id))
                    .Where(e => e != null)
                    .Select(e => new { id = e.Id.GetValue(), name = e.Name })
                    .ToList(),
            warnings = Warnings
        };
    }
}
