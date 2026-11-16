using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CloseFamilyCommand : FamilyEditorCommandBase
{
    public CloseFamilyCommand(UIApplication uiApp)
        : base(new CloseFamilyEventHandler(), uiApp)
    {
    }

    public override string CommandName => "close_family";
}
