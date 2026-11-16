using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilySweptBlendCommand : FamilyEditorCommandBase
{
    public CreateFamilySweptBlendCommand(UIApplication uiApp)
        : base(new CreateFamilySweptBlendEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_swept_blend";
}
