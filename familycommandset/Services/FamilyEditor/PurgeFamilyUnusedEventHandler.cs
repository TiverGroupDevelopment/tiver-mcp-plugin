using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>Удаление неиспользуемых элементов семейства (типы вложенных семейств, материалы, стили и т. п.).</summary>
public class PurgeFamilyUnusedEventHandler : FamilyEditorEventHandlerBase
{
    private const int MaxPasses = 5;

    public override string GetName() => "Purge family unused";

    protected override object Run(UIApplication app, JObject args)
    {
#if REVIT2024_OR_GREATER
        var dryRun = JsonArgs.OptionalBool(args, "dryRun", false);
        if (dryRun)
        {
            return Read(app, args, session => new
            {
                dryRun = true,
                unused = Unused(session.Document).Select(id => Describe(session.Document, id)).ToList()
            });
        }

        return Edit(app, args, "MCP: purge unused", doc =>
        {
            var deleted = new List<object>();
            var failed = new List<object>();

            // Удаление одних элементов освобождает другие, поэтому несколько проходов.
            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var unused = Unused(doc);
                if (unused.Count == 0) break;
                var deletedThisPass = 0;

                foreach (var id in unused)
                {
                    if (doc.GetElement(id) == null) continue;
                    var description = Describe(doc, id);
                    try
                    {
                        doc.Delete(id);
                        deleted.Add(description);
                        deletedThisPass++;
                    }
                    catch (Exception ex)
                    {
                        if (pass == 0) failed.Add(new { element = description, reason = ex.Message });
                    }
                }

                if (deletedThisPass == 0) break;
            }

            return new { deletedCount = deleted.Count, deleted, failed };
        });
#else
        throw new NotSupportedException(
            "Очистка неиспользуемого доступна через API начиная с Revit 2024 (Document.GetUnusedElements). В этой версии используйте «Управление → Удалить неиспользуемые» вручную.");
#endif
    }

#if REVIT2024_OR_GREATER
    private static List<ElementId> Unused(Document doc)
    {
        return doc.GetUnusedElements(new HashSet<ElementId>()).ToList();
    }

    private static object Describe(Document doc, ElementId id)
    {
        var element = doc.GetElement(id);
        return new
        {
            id = id.GetValue(),
            kind = element?.GetType().Name,
            name = element?.Name,
            category = element?.Category?.Name
        };
    }
#endif
}
