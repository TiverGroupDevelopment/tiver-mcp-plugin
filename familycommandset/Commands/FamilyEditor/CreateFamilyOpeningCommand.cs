using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyOpeningCommand : FamilyEditorCommandBase
{
    public CreateFamilyOpeningCommand(UIApplication uiApp)
        : base(new CreateFamilyOpeningEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_opening";
}
