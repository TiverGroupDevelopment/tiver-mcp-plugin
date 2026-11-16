using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class ManageFamilyLookupTablesCommand : FamilyEditorCommandBase
{
    public ManageFamilyLookupTablesCommand(UIApplication uiApp)
        : base(new ManageFamilyLookupTablesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "manage_family_lookup_tables";
}
