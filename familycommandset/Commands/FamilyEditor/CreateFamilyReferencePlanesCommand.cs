using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyReferencePlanesCommand : FamilyEditorCommandBase
{
    public CreateFamilyReferencePlanesCommand(UIApplication uiApp)
        : base(new CreateFamilyReferencePlanesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_reference_planes";
}
