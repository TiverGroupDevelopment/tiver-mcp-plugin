using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class SetFamilyParameterValuesCommand : FamilyEditorCommandBase
{
    public SetFamilyParameterValuesCommand(UIApplication uiApp)
        : base(new SetFamilyParameterValuesEventHandler(), uiApp)
    {
    }

    public override string CommandName => "set_family_parameter_values";
}
