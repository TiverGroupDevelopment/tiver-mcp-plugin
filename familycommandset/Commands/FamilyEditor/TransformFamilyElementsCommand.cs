using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class TransformFamilyElementsCommand : FamilyEditorCommandBase
{
    public TransformFamilyElementsCommand(UIApplication uiApp)
        : base(new TransformFamilyElementsEventHandler(), uiApp)
    {
    }

    public override string CommandName => "transform_family_elements";
}
