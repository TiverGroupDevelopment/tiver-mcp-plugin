using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Topography;

namespace RevitMCPCommandSet.Commands.Topography;

public class GetTopographyElevationCommand : TopographyCommandBase
{
    public GetTopographyElevationCommand(UIApplication uiApp)
        : base(new GetTopographyElevationEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_topography_elevation";
}
