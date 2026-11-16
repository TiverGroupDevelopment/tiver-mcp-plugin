using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class AlignFamilyElementsCommand : FamilyEditorCommandBase
{
    public AlignFamilyElementsCommand(UIApplication uiApp)
        : base(new AlignFamilyElementsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "align_family_elements";
}
