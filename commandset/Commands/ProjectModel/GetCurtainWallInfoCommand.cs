using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class GetCurtainWallInfoCommand : FamilyEditorCommandBase
{
    public GetCurtainWallInfoCommand(UIApplication uiApp)
        : base(new GetCurtainWallInfoEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_curtain_wall_info";
}
