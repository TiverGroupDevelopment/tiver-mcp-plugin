using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Services.ProjectModel;

/// <summary>Основа обработчиков модели проекта: транзакция с подавлением окон и откатом при ошибке.</summary>
public abstract class ProjectModelEventHandlerBase : FamilyEditorEventHandlerBase
{
    protected static T InTransaction<T>(Document doc, string name, Func<FailureSuppressor, T> action)
    {
        var failures = new FailureSuppressor();
        using var transaction = new Transaction(doc, name);
        failures.Attach(transaction);
        transaction.Start();
        T result;
        try
        {
            result = action(failures);
        }
        catch
        {
            if (transaction.HasStarted() && !transaction.HasEnded()) transaction.RollBack();
            throw;
        }

        var status = transaction.Commit();
        if (status != TransactionStatus.Committed || failures.HasErrors)
        {
            throw new InvalidOperationException($"Revit отменил '{name}' ({status}): {string.Join("; ", failures.Errors.DefaultIfEmpty("без сообщения"))}");
        }

        return result;
    }
}
