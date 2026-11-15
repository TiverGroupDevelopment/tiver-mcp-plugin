using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class ListProjectFamiliesCommand : FamilyEditorCommandBase
{
    public ListProjectFamiliesCommand(UIApplication uiApp)
        : base(new ListProjectFamiliesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "list_project_families";
}
