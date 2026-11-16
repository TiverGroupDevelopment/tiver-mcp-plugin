using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class NewFamilyCommand : FamilyEditorCommandBase
{
    public NewFamilyCommand(UIApplication uiApp)
        : base(new NewFamilyEventHandler(), uiApp)
    {
    }

    public override string CommandName => "new_family";
}
