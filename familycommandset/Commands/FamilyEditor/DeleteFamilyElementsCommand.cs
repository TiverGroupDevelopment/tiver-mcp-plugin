using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class DeleteFamilyElementsCommand : FamilyEditorCommandBase
{
    public DeleteFamilyElementsCommand(UIApplication uiApp)
        : base(new DeleteFamilyElementsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "delete_family_elements";
}
