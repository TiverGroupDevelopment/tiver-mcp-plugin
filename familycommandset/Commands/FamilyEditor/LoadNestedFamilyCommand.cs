using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class LoadNestedFamilyCommand : FamilyEditorCommandBase
{
    public LoadNestedFamilyCommand(UIApplication uiApp)
        : base(new LoadNestedFamilyEventHandler(), uiApp)
    {
    }

    public override string CommandName => "load_nested_family";
}
