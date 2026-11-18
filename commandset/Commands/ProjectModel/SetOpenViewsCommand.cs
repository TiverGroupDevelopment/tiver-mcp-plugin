using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.ProjectModel;
using RevitMCPFamilyCommandSet.Commands.FamilyEditor;

namespace RevitMCPCommandSet.Commands.ProjectModel;

public class SetOpenViewsCommand : FamilyEditorCommandBase
{
    public SetOpenViewsCommand(UIApplication uiApp)
        : base(new SetOpenViewsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "set_open_views";
}
