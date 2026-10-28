using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class CreateFamilyExtrusionCommand : FamilyEditorCommandBase
{
    public CreateFamilyExtrusionCommand(UIApplication uiApp)
        : base(new CreateFamilyExtrusionEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_extrusion";
}
