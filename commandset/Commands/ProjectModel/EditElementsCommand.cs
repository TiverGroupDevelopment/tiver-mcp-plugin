using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class EditElementsCommand : FamilyEditorCommandBase
{
    public EditElementsCommand(UIApplication uiApp)
        : base(new EditElementsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "edit_elements";
}
