using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class GetFamilyGeometryCommand : FamilyEditorCommandBase
{
    public GetFamilyGeometryCommand(UIApplication uiApp)
        : base(new GetFamilyGeometryEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_family_geometry";
}
