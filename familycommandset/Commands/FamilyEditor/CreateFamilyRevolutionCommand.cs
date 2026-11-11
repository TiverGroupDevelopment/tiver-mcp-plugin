using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyRevolutionCommand : FamilyEditorCommandBase
{
    public CreateFamilyRevolutionCommand(UIApplication uiApp)
        : base(new CreateFamilyRevolutionEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_revolution";
}
