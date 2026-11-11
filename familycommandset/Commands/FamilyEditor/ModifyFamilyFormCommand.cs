using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class ModifyFamilyFormCommand : FamilyEditorCommandBase
{
    public ModifyFamilyFormCommand(UIApplication uiApp)
        : base(new ModifyFamilyFormEventHandler(), uiApp)
    {
    }

    public override string CommandName => "modify_family_form";
}
