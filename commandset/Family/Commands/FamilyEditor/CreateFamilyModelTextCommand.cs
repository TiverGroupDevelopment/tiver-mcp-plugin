using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyModelTextCommand : FamilyEditorCommandBase
{
    public CreateFamilyModelTextCommand(UIApplication uiApp)
        : base(new CreateFamilyModelTextEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_model_text";
}
