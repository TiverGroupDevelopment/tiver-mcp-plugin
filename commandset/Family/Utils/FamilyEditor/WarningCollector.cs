using Autodesk.Revit.DB;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

public sealed class WarningCollector : IFailuresPreprocessor
{
    public List<string> Warnings { get; } = new List<string>();

    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        foreach (var failure in failuresAccessor.GetFailureMessages())
        {
            if (failure.GetSeverity() != FailureSeverity.Warning) continue;
            Warnings.Add(failure.GetDescriptionText());
            failuresAccessor.DeleteWarning(failure);
        }

        return FailureProcessingResult.Continue;
    }
}
