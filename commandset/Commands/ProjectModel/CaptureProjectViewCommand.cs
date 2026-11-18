using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class CaptureProjectViewCommand : FamilyEditorCommandBase
{
    public CaptureProjectViewCommand(UIApplication uiApp)
        : base(new CaptureProjectViewEventHandler(), uiApp)
    {
    }

    public override string CommandName => "capture_project_view";
}
