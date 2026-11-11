using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class SetFamilyPropertiesCommand : FamilyEditorCommandBase
{
    public SetFamilyPropertiesCommand(UIApplication uiApp)
        : base(new SetFamilyPropertiesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "set_family_properties";
}
