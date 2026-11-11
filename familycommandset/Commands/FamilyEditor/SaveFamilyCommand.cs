using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class SaveFamilyCommand : FamilyEditorCommandBase
{
    public SaveFamilyCommand(UIApplication uiApp)
        : base(new SaveFamilyEventHandler(), uiApp)
    {
    }

    public override string CommandName => "save_family";
}
