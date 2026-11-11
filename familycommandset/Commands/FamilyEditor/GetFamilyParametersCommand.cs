using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class GetFamilyParametersCommand : FamilyEditorCommandBase
{
    public GetFamilyParametersCommand(UIApplication uiApp)
        : base(new GetFamilyParametersEventHandler(), uiApp)
    {
    }

    public override string CommandName => "get_family_parameters";
}
