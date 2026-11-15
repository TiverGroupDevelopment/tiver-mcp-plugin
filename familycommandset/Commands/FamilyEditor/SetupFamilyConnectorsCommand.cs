using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class SetupFamilyConnectorsCommand : FamilyEditorCommandBase
{
    public SetupFamilyConnectorsCommand(UIApplication uiApp)
        : base(new SetupFamilyConnectorsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "setup_family_connectors";
}
