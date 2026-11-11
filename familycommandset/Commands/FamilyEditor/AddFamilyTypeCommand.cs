using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class AddFamilyTypeCommand : FamilyEditorCommandBase
{
    public AddFamilyTypeCommand(UIApplication uiApp)
        : base(new AddFamilyTypeEventHandler(), uiApp)
    {
    }

    public override string CommandName => "add_family_type";
}
