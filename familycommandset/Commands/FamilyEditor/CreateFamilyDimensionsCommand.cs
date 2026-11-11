using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyDimensionsCommand : FamilyEditorCommandBase
{
    public CreateFamilyDimensionsCommand(UIApplication uiApp)
        : base(new CreateFamilyDimensionsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_dimensions";
}
