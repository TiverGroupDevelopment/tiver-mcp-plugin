using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Topography;

namespace RevitMCPCommandSet.Commands.Topography;

public class GetTopographyCommand : TopographyCommandBase
{
    public GetTopographyCommand(UIApplication uiApp)
        : base(new GetTopographyEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_topography";
}
