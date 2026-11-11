using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyArrayCommand : FamilyEditorCommandBase
{
    public CreateFamilyArrayCommand(UIApplication uiApp)
        : base(new CreateFamilyArrayEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_array";
}
