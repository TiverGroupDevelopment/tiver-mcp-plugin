using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class UpdateFamilyParametersCommand : FamilyEditorCommandBase
{
    public UpdateFamilyParametersCommand(UIApplication uiApp)
        : base(new UpdateFamilyParametersEventHandler(), uiApp)
    {
    }

    public override string CommandName => "update_family_parameters";
}
