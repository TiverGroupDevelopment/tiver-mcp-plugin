using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class AddFamilyParametersCommand : FamilyEditorCommandBase
{
    public AddFamilyParametersCommand(UIApplication uiApp)
        : base(new AddFamilyParametersEventHandler(), uiApp)
    {
    }

    public override string CommandName => "add_family_parameters";
}
