using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class LoadFamilyIntoProjectCommand : FamilyEditorCommandBase
{
    public LoadFamilyIntoProjectCommand(UIApplication uiApp)
        : base(new LoadFamilyIntoProjectEventHandler(), uiApp)
    {
    }

    public override string CommandName => "load_family_into_project";
}
