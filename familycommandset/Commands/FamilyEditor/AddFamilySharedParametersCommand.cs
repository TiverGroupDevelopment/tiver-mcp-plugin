using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class AddFamilySharedParametersCommand : FamilyEditorCommandBase
{
    public AddFamilySharedParametersCommand(UIApplication uiApp)
        : base(new AddFamilySharedParametersEventHandler(), uiApp)
    {
    }

    public override string CommandName => "add_family_shared_parameters";
}
