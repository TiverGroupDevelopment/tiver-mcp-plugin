using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilySubcategoriesCommand : FamilyEditorCommandBase
{
    public CreateFamilySubcategoriesCommand(UIApplication uiApp)
        : base(new CreateFamilySubcategoriesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_subcategories";
}
