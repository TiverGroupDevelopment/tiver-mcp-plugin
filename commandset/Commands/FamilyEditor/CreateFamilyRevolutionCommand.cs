using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class CreateFamilyRevolutionCommand : FamilyEditorCommandBase
{
    public CreateFamilyRevolutionCommand(UIApplication uiApp)
        : base(new CreateFamilyRevolutionEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_revolution";
}
