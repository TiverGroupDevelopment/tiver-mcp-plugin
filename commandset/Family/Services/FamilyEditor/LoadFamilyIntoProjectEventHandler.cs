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

        var replaced = ReplaceIfCategoryChanged(familyDoc, project);
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
            replacedFamilyWithOtherCategory = replaced,
            warnings = Warnings
        };
    }

    /// <summary>
    /// Revit не перезагружает семейство, если у него сменилась категория. Если в проекте старое семейство
    /// без экземпляров — удаляем его и грузим заново; с экземплярами — объясняем, что делать.
    /// </summary>
    private string ReplaceIfCategoryChanged(Document familyDoc, Document project)
    {
        var name = FamilySessionRegistry.Current?.Document == familyDoc
            ? FamilySessionRegistry.Current.FamilyName
            : FamilySessionRegistry.StripExtension(familyDoc.Title);
        var existing = new FilteredElementCollector(project)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        var newCategory = familyDoc.OwnerFamily?.FamilyCategory;
        if (existing == null || newCategory == null || existing.FamilyCategory == null) return null;
        if (existing.FamilyCategory.Id.GetValue() == newCategory.Id.GetValue()) return null;

        var instances = new FilteredElementCollector(project)
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Count(i => i.Symbol?.Family?.Id == existing.Id);
        if (instances > 0)
        {
            throw new InvalidOperationException(
                $"В проекте '{project.Title}' семейство '{name}' имеет категорию «{existing.FamilyCategory.Name}» и {instances} экз.; " +
                $"Revit не перезагрузит его с категорией «{newCategory.Name}». Удалите экземпляры или переименуйте семейство (save_family с новым path).");
        }

        var oldCategory = existing.FamilyCategory.Name;
        var failures = new FailureSuppressor();
        using (var transaction = new Transaction(project, "MCP: удалить семейство со старой категорией"))
        {
            failures.Attach(transaction);
            transaction.Start();
            project.Delete(existing.Id);
            if (transaction.Commit() != TransactionStatus.Committed)
            {
                throw new InvalidOperationException($"Не удалось удалить старое семейство '{name}' из проекта: {string.Join("; ", failures.Errors)}");
            }
        }

        return $"{name}: «{oldCategory}» → «{newCategory.Name}»";
    }
}
