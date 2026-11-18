using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class GetElementTypesCommand : FamilyEditorCommandBase
{
    public GetElementTypesCommand(UIApplication uiApp)
        : base(new GetElementTypesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_element_types";
}
