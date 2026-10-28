using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class AssociateFamilyParametersCommand : FamilyEditorCommandBase
{
    public AssociateFamilyParametersCommand(UIApplication uiApp)
        : base(new AssociateFamilyParametersEventHandler(), uiApp)
    {
    }

    public override string CommandName => "associate_family_parameters";
}
