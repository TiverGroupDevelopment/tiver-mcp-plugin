using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyMaterialsCommand : FamilyEditorCommandBase
{
    public CreateFamilyMaterialsCommand(UIApplication uiApp)
        : base(new CreateFamilyMaterialsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_materials";
}
