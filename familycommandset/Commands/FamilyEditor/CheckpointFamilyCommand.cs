using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CheckpointFamilyCommand : FamilyEditorCommandBase
{
    public CheckpointFamilyCommand(UIApplication uiApp)
        : base(new CheckpointFamilyEventHandler(), uiApp)
    {
    }

    public override string CommandName => "checkpoint_family";
}
