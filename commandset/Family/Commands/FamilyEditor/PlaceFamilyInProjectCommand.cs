using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class PlaceFamilyInProjectCommand : FamilyEditorCommandBase
{
    public PlaceFamilyInProjectCommand(UIApplication uiApp)
        : base(new PlaceFamilyInProjectEventHandler(), uiApp)
    {
    }

    public override string CommandName => "place_family_in_project";
}
