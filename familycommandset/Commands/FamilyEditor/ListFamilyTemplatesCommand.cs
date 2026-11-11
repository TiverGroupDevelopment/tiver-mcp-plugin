using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class ListFamilyTemplatesCommand : FamilyEditorCommandBase
{
    public ListFamilyTemplatesCommand(UIApplication uiApp)
        : base(new ListFamilyTemplatesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "list_family_templates";
}
