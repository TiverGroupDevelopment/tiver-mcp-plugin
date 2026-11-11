using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyConnectorCommand : FamilyEditorCommandBase
{
    public CreateFamilyConnectorCommand(UIApplication uiApp)
        : base(new CreateFamilyConnectorEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_connector";
}
