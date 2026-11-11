using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class GetFamilyPropertiesEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Get family properties";

    protected override object Run(UIApplication app, JObject args)
    {
        return Read(app, args, session => FamilyProperties.Describe(session.Document));
    }
}
