using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class EditElementTypeCommand : FamilyEditorCommandBase
{
    public EditElementTypeCommand(UIApplication uiApp)
        : base(new EditElementTypeEventHandler(), uiApp)
    {
    }

    public override string CommandName => "edit_element_type";
}
