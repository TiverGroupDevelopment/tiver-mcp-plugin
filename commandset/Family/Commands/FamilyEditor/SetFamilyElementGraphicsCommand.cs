using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class SetFamilyElementGraphicsCommand : FamilyEditorCommandBase
{
    public SetFamilyElementGraphicsCommand(UIApplication uiApp)
        : base(new SetFamilyElementGraphicsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "set_family_element_graphics";
}
