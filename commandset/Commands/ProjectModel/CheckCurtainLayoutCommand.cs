using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class CheckCurtainLayoutCommand : FamilyEditorCommandBase
{
    public CheckCurtainLayoutCommand(UIApplication uiApp)
        : base(new CheckCurtainLayoutEventHandler(), uiApp)
    {
    }

    public override string CommandName => "check_curtain_layout";
}
