using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CaptureFamilyViewCommand : FamilyEditorCommandBase
{
    public CaptureFamilyViewCommand(UIApplication uiApp)
        : base(new CaptureFamilyViewEventHandler(), uiApp)
    {
    }

    public override string CommandName => "capture_family_view";
}
