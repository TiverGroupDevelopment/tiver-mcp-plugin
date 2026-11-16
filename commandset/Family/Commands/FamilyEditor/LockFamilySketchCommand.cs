using Autodesk.Revit.UI;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public class LockFamilySketchCommand : FamilyEditorCommandBase
{
    public LockFamilySketchCommand(UIApplication uiApp)
        : base(new LockFamilySketchEventHandler(), uiApp)
    {
    }

    public override string CommandName => "lock_family_sketch";
}
