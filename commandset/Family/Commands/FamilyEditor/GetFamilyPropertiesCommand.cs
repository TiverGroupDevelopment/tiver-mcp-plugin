using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class GetFamilyPropertiesCommand : FamilyEditorCommandBase
{
    public GetFamilyPropertiesCommand(UIApplication uiApp)
        : base(new GetFamilyPropertiesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_family_properties";
}
