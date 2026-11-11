using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class RestoreFamilyCheckpointCommand : FamilyEditorCommandBase
{
    public RestoreFamilyCheckpointCommand(UIApplication uiApp)
        : base(new RestoreFamilyCheckpointEventHandler(), uiApp)
    {
    }

    public override string CommandName => "restore_family_checkpoint";
}
