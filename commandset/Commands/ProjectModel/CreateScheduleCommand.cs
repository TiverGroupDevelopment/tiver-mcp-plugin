using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class CreateScheduleCommand : FamilyEditorCommandBase
{
    public CreateScheduleCommand(UIApplication uiApp)
        : base(new CreateScheduleEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_schedule";
}
