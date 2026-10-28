using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Topography;

namespace RevitMCPCommandSet.Commands.Topography;

public class ConvertTopographyToToposolidCommand : TopographyCommandBase
{
    public ConvertTopographyToToposolidCommand(UIApplication uiApp)
        : base(new ConvertTopographyToToposolidEventHandler(), uiApp)
    {
    }

    public override string CommandName => "convert_topography_to_toposolid";
}
