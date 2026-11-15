using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class GetFamilyMassCommand : FamilyEditorCommandBase
{
    public GetFamilyMassCommand(UIApplication uiApp)
        : base(new GetFamilyMassEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_family_mass";
}
