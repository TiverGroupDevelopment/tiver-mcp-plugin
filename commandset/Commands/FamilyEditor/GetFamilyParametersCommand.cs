using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class GetFamilyParametersCommand : FamilyEditorCommandBase
{
    public GetFamilyParametersCommand(UIApplication uiApp)
        : base(new GetFamilyParametersEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_family_parameters";
}
