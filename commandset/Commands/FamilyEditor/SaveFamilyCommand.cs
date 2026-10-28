using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class SaveFamilyCommand : FamilyEditorCommandBase
{
    public SaveFamilyCommand(UIApplication uiApp)
        : base(new SaveFamilyEventHandler(), uiApp)
    {
    }

    public override string CommandName => "save_family";
}
