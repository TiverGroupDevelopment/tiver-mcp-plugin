using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Topography;

namespace RevitMCPCommandSet.Commands.Topography;

public class EditTopographyPointsCommand : TopographyCommandBase
{
    public EditTopographyPointsCommand(UIApplication uiApp)
        : base(new EditTopographyPointsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "edit_topography_points";
}
