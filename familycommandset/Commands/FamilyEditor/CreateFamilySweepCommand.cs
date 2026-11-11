using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilySweepCommand : FamilyEditorCommandBase
{
    public CreateFamilySweepCommand(UIApplication uiApp)
        : base(new CreateFamilySweepEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_sweep";
}
