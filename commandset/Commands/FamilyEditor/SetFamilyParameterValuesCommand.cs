using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.FamilyEditor;

namespace RevitMCPCommandSet.Commands.FamilyEditor;

public class SetFamilyParameterValuesCommand : FamilyEditorCommandBase
{
    public SetFamilyParameterValuesCommand(UIApplication uiApp)
        : base(new SetFamilyParameterValuesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "set_family_parameter_values";
}
