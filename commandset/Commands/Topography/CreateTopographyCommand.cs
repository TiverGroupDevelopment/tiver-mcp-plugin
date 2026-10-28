using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Topography;

namespace RevitMCPCommandSet.Commands.Topography;

public class CreateTopographyCommand : TopographyCommandBase
{
    public CreateTopographyCommand(UIApplication uiApp)
        : base(new CreateTopographyEventHandler(), uiApp)
    {
    }

    public override string CommandName => "create_topography";
}
