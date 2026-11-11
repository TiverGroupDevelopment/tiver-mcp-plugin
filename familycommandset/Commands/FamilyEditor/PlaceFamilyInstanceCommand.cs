using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class PlaceFamilyInstanceCommand : FamilyEditorCommandBase
{
    public PlaceFamilyInstanceCommand(UIApplication uiApp)
        : base(new PlaceFamilyInstanceEventHandler(), uiApp)
    {
    }

    public override string CommandName => "place_family_instance";
}
