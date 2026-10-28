using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class AddFamilyParametersCommand : FamilyEditorCommandBase
{
    public AddFamilyParametersCommand(UIApplication uiApp)
        : base(new AddFamilyParametersEventHandler(), uiApp)
    {
    }

    public override string CommandName => "add_family_parameters";
}
