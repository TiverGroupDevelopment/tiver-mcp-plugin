using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class SetFamilyParameterFormulasCommand : FamilyEditorCommandBase
{
    public SetFamilyParameterFormulasCommand(UIApplication uiApp)
        : base(new SetFamilyParameterFormulasEventHandler(), uiApp)
    {
    }

    public override string CommandName => "set_family_parameter_formulas";
}
