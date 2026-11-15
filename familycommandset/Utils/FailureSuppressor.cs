using Autodesk.Revit.DB;

namespace RevitMCPFamilyCommandSet.Utils;

/// <summary>
/// Подавляет диалоги Revit: предупреждения удаляются, ошибки собираются и приводят к откату транзакции.
/// </summary>
public sealed class FailureSuppressor : IFailuresPreprocessor
{
    public List<string> Warnings { get; } = new List<string>();
    public List<string> Errors { get; } = new List<string>();

    public bool HasErrors => Errors.Count > 0;

    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        return Process(failuresAccessor);
    }

    /// <summary>
    /// Используется и как препроцессор транзакции, и из события Application.FailuresProcessing.
    /// </summary>
    public FailureProcessingResult Process(FailuresAccessor failuresAccessor)
    {
        var failures = failuresAccessor?.GetFailureMessages();
        if (failures == null || failures.Count == 0) return FailureProcessingResult.Continue;

        var hasErrors = false;
        foreach (var failure in failures)
        {
            if (failure == null) continue;

            var message = failure.GetDescriptionText();
            if (failure.GetSeverity() == FailureSeverity.Warning)
            {
                AddUnique(Warnings, message);
                failuresAccessor.DeleteWarning(failure);
                continue;
            }

            hasErrors = true;
            AddUnique(Errors, message);
        }

        if (!hasErrors) return FailureProcessingResult.Continue;

        // Без ClearAfterRollback Revit после отката всё равно показывает ошибки в модальном окне,
        // и команда висит до таймаута, пока пользователь его не закроет.
        try
        {
            var options = failuresAccessor.GetFailureHandlingOptions();
            options.SetClearAfterRollback(true);
            failuresAccessor.SetFailureHandlingOptions(options);
        }
        catch (Exception)
        {
        }

        return FailureProcessingResult.ProceedWithRollBack;
    }

    /// <summary>Настройки транзакции: этот препроцессор и очистка сбоев после отката.</summary>
    public void Attach(Transaction transaction)
    {
        var options = transaction.GetFailureHandlingOptions();
        options.SetFailuresPreprocessor(this);
        options.SetClearAfterRollback(true);
        transaction.SetFailureHandlingOptions(options);
    }

    private static void AddUnique(List<string> target, string message)
    {
        if (!string.IsNullOrWhiteSpace(message) && !target.Contains(message)) target.Add(message);
    }
}
