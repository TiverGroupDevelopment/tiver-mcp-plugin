using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>Коннектор инженерных систем на плоской грани формы.</summary>
public class CreateFamilyConnectorEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family connector";

    protected override object Run(UIApplication app, JObject args)
    {
        var domain = JsonArgs.RequiredString(args, "domain").ToLowerInvariant();
        var faceSpec = JsonArgs.RequiredObject(args, "face");
        var systemType = JsonArgs.OptionalString(args, "systemType");

        return Edit(app, args, "MCP: create family connector", doc =>
        {
            if (!JsonArgs.Has(faceSpec, "elementId") || !JsonArgs.Has(faceSpec, "faceIndex"))
            {
                throw new ArgumentException("face должен быть {elementId, faceIndex} — плоская грань формы (get_family_geometry с detail: true)");
            }

            var face = FamilyGeometryReader.Resolve(doc, faceSpec);
            ConnectorElement connector;
            switch (domain)
            {
                case "duct":
                    connector = ConnectorElement.CreateDuctConnector(
                        doc,
                        ParseEnum(systemType, DuctSystemType.Global),
                        ParseEnum(JsonArgs.OptionalString(args, "shape"), ConnectorProfileType.Round),
                        face.Reference);
                    break;
                case "pipe":
                    connector = ConnectorElement.CreatePipeConnector(doc, ParseEnum(systemType, PipeSystemType.Global), face.Reference);
                    break;
                case "electrical":
                    connector = ConnectorElement.CreateElectricalConnector(doc, ParseEnum(systemType, ElectricalSystemType.PowerBalanced), face.Reference);
                    break;
                case "conduit":
                    connector = ConnectorElement.CreateConduitConnector(doc, face.Reference);
                    break;
                case "cabletray":
                    connector = ConnectorElement.CreateCableTrayConnector(doc, face.Reference);
                    break;
                default:
                    throw new ArgumentException($"Неизвестный domain '{domain}'. Допустимо: duct, pipe, electrical, conduit, cabletray");
            }

            if (connector == null) throw new InvalidOperationException("Revit не создал коннектор");

            var notes = new List<string>();
            SetLength(connector, args, "diameter", 0.5, notes, BuiltInParameter.CONNECTOR_RADIUS);
            SetLength(connector, args, "width", 1, notes, BuiltInParameter.CONNECTOR_WIDTH);
            SetLength(connector, args, "height", 1, notes, BuiltInParameter.CONNECTOR_HEIGHT);

            doc.Regenerate();
            return new
            {
                id = connector.Id.GetValue(),
                domain,
                face = face.Description,
                notes,
                associableParameters = FamilyFormUtils.AssociableParameters(doc.FamilyManager, connector)
            };
        });
    }

    private static void SetLength(ConnectorElement connector, JObject args, string key, double factor, List<string> notes, BuiltInParameter builtIn)
    {
        if (!JsonArgs.Has(args, key)) return;
        var parameter = connector.get_Parameter(builtIn);
        if (parameter == null || parameter.IsReadOnly)
        {
            notes.Add($"'{key}' не задан: у этого коннектора нет такого размера (проверьте shape)");
            return;
        }

        parameter.Set(JsonArgs.RequiredDouble(args, key) * factor / 304.8);
    }

    private static T ParseEnum<T>(string value, T fallback) where T : struct
    {
        if (value == null) return fallback;
        if (Enum.TryParse(value, true, out T parsed) && !int.TryParse(value, out _)) return parsed;
        throw new ArgumentException($"Неизвестное значение '{value}'. Допустимо: {string.Join(", ", Enum.GetNames(typeof(T)))}");
    }
}
