using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Topography;

namespace RevitMCPCommandSet.Commands.Topography;

public class CreateTopographySubregionCommand : TopographyCommandBase
{
    public CreateTopographySubregionCommand(UIApplication uiApp)
        : base(new CreateTopographySubregionEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_topography_subregion";
}
