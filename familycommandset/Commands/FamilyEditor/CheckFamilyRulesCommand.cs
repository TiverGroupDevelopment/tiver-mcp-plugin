using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CheckFamilyRulesCommand : FamilyEditorCommandBase
{
    public CheckFamilyRulesCommand(UIApplication uiApp)
        : base(new CheckFamilyRulesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "check_family_rules";
}
