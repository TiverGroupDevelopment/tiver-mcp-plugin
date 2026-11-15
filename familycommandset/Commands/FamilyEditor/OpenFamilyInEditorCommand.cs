using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class OpenFamilyInEditorCommand : FamilyEditorCommandBase
{
    public OpenFamilyInEditorCommand(UIApplication uiApp)
        : base(new OpenFamilyInEditorEventHandler(), uiApp)
    {
    }

    public override string CommandName => "open_family_in_editor";
}
