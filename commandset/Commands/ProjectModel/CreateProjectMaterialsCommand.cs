using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class CreateProjectMaterialsCommand : FamilyEditorCommandBase
{
    public CreateProjectMaterialsCommand(UIApplication uiApp)
        : base(new CreateProjectMaterialsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_project_materials";
}
