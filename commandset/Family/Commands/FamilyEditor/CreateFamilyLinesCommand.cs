using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyLinesCommand : FamilyEditorCommandBase
{
    public CreateFamilyLinesCommand(UIApplication uiApp)
        : base(new CreateFamilyLinesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_lines";
}
