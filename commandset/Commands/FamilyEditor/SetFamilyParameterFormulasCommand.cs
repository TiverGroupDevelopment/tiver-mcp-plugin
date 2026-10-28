using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class SetFamilyParameterFormulasCommand : FamilyEditorCommandBase
{
    public SetFamilyParameterFormulasCommand(UIApplication uiApp)
        : base(new SetFamilyParameterFormulasEventHandler(), uiApp)
    {
    }

    public override string CommandName => "set_family_parameter_formulas";
}
