using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class PurgeFamilyUnusedCommand : FamilyEditorCommandBase
{
    public PurgeFamilyUnusedCommand(UIApplication uiApp)
        : base(new PurgeFamilyUnusedEventHandler(), uiApp)
    {
    }

    public override string CommandName => "purge_family_unused";
}
