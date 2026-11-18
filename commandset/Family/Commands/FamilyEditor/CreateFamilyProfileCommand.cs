using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyProfileCommand : FamilyEditorCommandBase
{
    public CreateFamilyProfileCommand(UIApplication uiApp)
        : base(new CreateFamilyProfileEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_profile";
}
