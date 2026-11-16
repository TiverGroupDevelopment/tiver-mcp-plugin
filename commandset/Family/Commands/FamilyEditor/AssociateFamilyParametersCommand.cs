using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class AssociateFamilyParametersCommand : FamilyEditorCommandBase
{
    public AssociateFamilyParametersCommand(UIApplication uiApp)
        : base(new AssociateFamilyParametersEventHandler(), uiApp)
    {
    }

    public override string CommandName => "associate_family_parameters";
}
