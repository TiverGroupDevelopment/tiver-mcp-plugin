using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class SetWallLayersCommand : FamilyEditorCommandBase
{
    public SetWallLayersCommand(UIApplication uiApp)
        : base(new SetWallLayersEventHandler(), uiApp)
    {
    }

    public override string CommandName => "set_wall_layers";
}
