using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class CreateFamilyBlendCommand : FamilyEditorCommandBase
{
    public CreateFamilyBlendCommand(UIApplication uiApp)
        : base(new CreateFamilyBlendEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_blend";
}
