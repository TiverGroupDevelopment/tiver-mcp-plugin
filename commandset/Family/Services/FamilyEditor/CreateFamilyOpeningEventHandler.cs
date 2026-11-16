using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>Проём в основе семейства (стена, перекрытие, потолок, крыша шаблона на основе).</summary>
public class CreateFamilyOpeningEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family opening";

    protected override object Run(UIApplication app, JObject args)
    {
        var profileSpec = JsonArgs.Required(args, "profile");

        return Edit(app, args, "MCP: create family opening", doc =>
        {
            Element host;
            if (JsonArgs.Has(args, "hostId"))
            {
                var hostId = args["hostId"].Value<long>();
                host = doc.GetElement(FamilyEditorIds.FromNumber(hostId))
                       ?? throw new ArgumentException($"Основа {hostId} не найдена");
            }
            else
            {
                host = new FilteredElementCollector(doc)
                           .WhereElementIsNotElementType()
                           .ToElements()
                           .FirstOrDefault(e => e is HostObject)
                       ?? throw new InvalidOperationException(
                           "В семействе нет основы (стены, перекрытия, потолка или крыши). Проём создаётся только в шаблонах «на основе».");
            }

            var plane = FamilyWorkPlane.Create(doc, args["workPlane"], host is Wall ? "XZ" : "XY");
            var profile = FamilyProfileBuilder.BuildLoop(doc, plane, profileSpec);
            var opening = doc.FamilyCreate.NewOpening(host, profile)
                          ?? throw new InvalidOperationException("Revit не создал проём");

            doc.Regenerate();
            return new
            {
                id = opening.Id.GetValue(),
                host = new { id = host.Id.GetValue(), kind = host.GetType().Name },
                workPlane = plane.Describe(),
                faces = FamilyGeometryReader.DescribeFaces(opening)
            };
        });
    }
}
