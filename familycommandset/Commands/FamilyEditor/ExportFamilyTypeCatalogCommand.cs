using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class ExportFamilyTypeCatalogCommand : FamilyEditorCommandBase
{
    public ExportFamilyTypeCatalogCommand(UIApplication uiApp)
        : base(new ExportFamilyTypeCatalogEventHandler(), uiApp)
    {
    }

    public override string CommandName => "export_family_type_catalog";
}
