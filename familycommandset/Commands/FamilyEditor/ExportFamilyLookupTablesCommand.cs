using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class ExportFamilyLookupTablesCommand : FamilyEditorCommandBase
{
    public ExportFamilyLookupTablesCommand(UIApplication uiApp)
        : base(new ExportFamilyLookupTablesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "export_family_lookup_tables";
}
