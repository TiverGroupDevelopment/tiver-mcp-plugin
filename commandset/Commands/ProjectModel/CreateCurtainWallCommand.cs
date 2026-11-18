using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class CreateCurtainWallCommand : FamilyEditorCommandBase
{
    public CreateCurtainWallCommand(UIApplication uiApp)
        : base(new CreateCurtainWallEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_curtain_wall";
}
