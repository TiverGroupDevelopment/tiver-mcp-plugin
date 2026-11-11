using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class TestFamilyFlexCommand : FamilyEditorCommandBase
{
    public TestFamilyFlexCommand(UIApplication uiApp)
        : base(new TestFamilyFlexEventHandler(), uiApp)
    {
    }

    public override string CommandName => "test_family_flex";
}
