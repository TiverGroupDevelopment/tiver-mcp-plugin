using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class CreateFamilyInspectionCopyCommand : FamilyEditorCommandBase
{
    public CreateFamilyInspectionCopyCommand(UIApplication uiApp)
        : base(new CreateFamilyInspectionCopyEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_family_inspection_copy";
}
