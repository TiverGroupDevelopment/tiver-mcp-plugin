using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class AddFamilyTypeCommand : FamilyEditorCommandBase
{
    public AddFamilyTypeCommand(UIApplication uiApp)
        : base(new AddFamilyTypeEventHandler(), uiApp)
    {
    }

    public override string CommandName => "add_family_type";
}
