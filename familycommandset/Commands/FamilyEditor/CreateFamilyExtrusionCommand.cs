using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyExtrusionCommand : FamilyEditorCommandBase
{
    public CreateFamilyExtrusionCommand(UIApplication uiApp)
        : base(new CreateFamilyExtrusionEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_extrusion";
}
