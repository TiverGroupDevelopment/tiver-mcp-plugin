using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class OpenFamilyCommand : FamilyEditorCommandBase
{
    public OpenFamilyCommand(UIApplication uiApp)
        : base(new OpenFamilyEventHandler(), uiApp)
    {
    }

    public override string CommandName => "open_family";
}
