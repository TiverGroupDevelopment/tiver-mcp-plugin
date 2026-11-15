using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Настройка коннекторов семейства: связь попарно (поток проходит через деталь), главный коннектор,
/// классификация системы (например Fitting для соединительных деталей), разворот направления и поворот
/// вокруг собственной оси, чтобы ось Y коннектора смотрела вверх (+Z семейства).
/// </summary>
public class SetupFamilyConnectorsEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Setup family connectors";

    protected override object Run(UIApplication app, JObject args)
    {
        return Edit(app, args, "MCP: настройка коннекторов", doc =>
        {
            var all = new FilteredElementCollector(doc).OfClass(typeof(ConnectorElement)).Cast<ConnectorElement>().ToList();
            if (all.Count == 0) throw new InvalidOperationException("В семействе нет коннекторов (create_family_connector)");
            var byId = all.ToDictionary(c => c.Id.GetValue());
            ConnectorElement Get(long id) => byId.TryGetValue(id, out var c) ? c : throw new ArgumentException($"Коннектор {id} не найден. Есть: {string.Join(", ", byId.Keys)}");

            var notes = new List<string>();

            if (args["flip"] is JArray flip)
            {
                foreach (var id in flip.Select(t => t.Value<long>())) Get(id).FlipDirection();
                doc.Regenerate();
            }

            var system = JsonArgs.OptionalString(args, "systemClassification");
            if (system != null)
            {
                if (!Enum.TryParse(system, true, out MEPSystemClassification classification))
                {
                    throw new ArgumentException($"Неизвестная классификация '{system}'. Примеры: Fitting, Global, DomesticColdWater, SupplyAir");
                }

                foreach (var connector in all)
                {
                    if (connector.IsSystemClassificationValid(classification)) connector.SystemClassification = classification;
                    else notes.Add($"коннектор {connector.Id.GetValue()} ({connector.Domain}): классификация {classification} недопустима");
                }
            }

            // Пары для связи: явно (link) или единственная пара, если коннекторов ровно два.
            var pairs = new List<(ConnectorElement, ConnectorElement)>();
            if (args["link"] is JArray link)
            {
                foreach (var pair in link.OfType<JArray>())
                {
                    if (pair.Count != 2) throw new ArgumentException("link: каждая пара — два id коннекторов");
                    pairs.Add((Get(pair[0].Value<long>()), Get(pair[1].Value<long>())));
                }
            }
            else if (all.Count == 2 && JsonArgs.OptionalBool(args, "autoLink", true))
            {
                pairs.Add((all[0], all[1]));
            }

            foreach (var (a, b) in pairs)
            {
                if (a.Domain != b.Domain) throw new ArgumentException($"Коннекторы {a.Id.GetValue()} и {b.Id.GetValue()} разных доменов — связать нельзя");
                a.SetLinkedConnectorElement(b);
            }

            var primaryId = JsonArgs.Has(args, "primary") ? args["primary"].Value<long>() : (pairs.Count > 0 ? pairs[0].Item1.Id.GetValue() : (long?)null);
            if (primaryId.HasValue) Get(primaryId.Value).AssignAsPrimary();

            if (JsonArgs.OptionalBool(args, "orientUp", true))
            {
                var up = JsonArgs.Has(args, "up") ? JsonArgs.Vector(args["up"], "up").Normalize() : XYZ.BasisZ;
                doc.Regenerate();
                foreach (var connector in all) notes.AddRange(OrientUp(doc, connector, up));
            }

            doc.Regenerate();
            return new { connectors = all.Select(Describe).ToList(), notes };
        });
    }

    /// <summary>Поворачивает коннектор вокруг его оси Z так, чтобы ось Y совпала с проекцией up.</summary>
    private static IEnumerable<string> OrientUp(Document doc, ConnectorElement connector, XYZ up)
    {
        var system = connector.CoordinateSystem;
        var z = system.BasisZ.Normalize();
        var target = up - z * up.DotProduct(z);
        if (target.GetLength() < 1e-6)
        {
            yield return $"коннектор {connector.Id.GetValue()} смотрит вдоль «вверх»: ось Y не задаётся, пропущен";
            yield break;
        }

        target = target.Normalize();
        var y = system.BasisY.Normalize();
        var angle = Math.Atan2(z.DotProduct(y.CrossProduct(target)), y.DotProduct(target));
        if (Math.Abs(angle) < 1e-6) yield break;

        string error = null;
        try
        {
            ElementTransformUtils.RotateElement(doc, connector.Id, Line.CreateBound(system.Origin, system.Origin + z), angle);
            doc.Regenerate();
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        if (error != null)
        {
            yield return $"коннектор {connector.Id.GetValue()}: Revit не дал повернуть ({error})";
            yield break;
        }

        var after = connector.CoordinateSystem.BasisY.Normalize();
        if (after.DotProduct(target) < 0.99)
        {
            yield return $"коннектор {connector.Id.GetValue()}: после поворота ось Y = [{Math.Round(after.X, 3)}, {Math.Round(after.Y, 3)}, {Math.Round(after.Z, 3)}]";
        }
    }

    private static object Describe(ConnectorElement c)
    {
        var system = c.CoordinateSystem;
        var linked = c.GetLinkedConnectorElement();
        return new
        {
            id = c.Id.GetValue(),
            domain = c.Domain.ToString(),
            systemClassification = c.SystemClassification.ToString(),
            primary = c.IsPrimary,
            linkedTo = linked?.Id.GetValue(),
            originMm = FamilyDocInfo.Mm(system.Origin),
            directionZ = FamilyDocInfo.Unit(system.BasisZ),
            axisY = FamilyDocInfo.Unit(system.BasisY)
        };
    }
}
