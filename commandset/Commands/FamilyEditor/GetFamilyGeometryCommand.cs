using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class GetFamilyGeometryCommand : FamilyEditorCommandBase
{
    public GetFamilyGeometryCommand(UIApplication uiApp)
        : base(new GetFamilyGeometryEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_family_geometry";
}
