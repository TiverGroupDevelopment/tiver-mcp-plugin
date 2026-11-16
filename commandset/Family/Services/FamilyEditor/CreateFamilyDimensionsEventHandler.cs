using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class CreateFamilyDimensionsEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family dimensions";

    protected override object Run(UIApplication app, JObject args)
    {
        var specs = JsonArgs.RequiredArray(args, "dimensions").OfType<JObject>().ToList();
        if (specs.Count == 0) throw new ArgumentException("dimensions пуст");

        return Edit(app, args, "MCP: create family dimensions", doc =>
        {
            var manager = doc.FamilyManager;
            var created = new List<object>();

            foreach (var spec in specs)
            {
                var references = JsonArgs.RequiredArray(spec, "references")
                    .Select(token => FamilyGeometryReader.Resolve(doc, token))
                    .ToList();
                if (references.Count < 2) throw new ArgumentException("Для размера нужно минимум две ссылки");

                var direction = references[0].Normal;
                var stray = references.FirstOrDefault(r => !FamilyGeometryReader.AreParallel(direction, r.Normal));
                if (stray != null)
                {
                    throw new ArgumentException(
                        $"Ссылки размера должны быть параллельны: '{stray.Description}' не параллельна '{references[0].Description}'");
                }

                var view = FamilyDocInfo.PickViewForNormal(doc, direction, JsonArgs.OptionalString(spec, "view"));
                var viewDirection = view.ViewDirection.Normalize();
                var side = viewDirection.CrossProduct(direction).Normalize();

                var basePoint = references[0].Point;
                var positions = references.Select(r => (r.Point - basePoint).DotProduct(direction)).ToList();
                var min = positions.Min();
                var max = positions.Max();
                if (max - min < doc.Application.ShortCurveTolerance)
                {
                    throw new ArgumentException("Ссылки размера совпадают: расстояние между ними равно нулю");
                }

                // Линия размера лежит в плоскости вида, смещена на offset от первой ссылки.
                var inView = basePoint - viewDirection * (basePoint - view.Origin).DotProduct(viewDirection);
                var anchor = inView + side * (JsonArgs.OptionalDouble(spec, "offset", 0) / 304.8);
                var line = Line.CreateBound(anchor + direction * min, anchor + direction * max);

                var array = new ReferenceArray();
                foreach (var reference in references) array.Append(reference.Reference);

                var dimension = doc.FamilyCreate.NewLinearDimension(view, line, array)
                                ?? throw new InvalidOperationException("Revit не создал размер");

                if (JsonArgs.OptionalBool(spec, "equal", false))
                {
                    if (references.Count < 3) throw new ArgumentException("equal (EQ) требует минимум три ссылки");
                    dimension.AreSegmentsEqual = true;
                }

                var label = JsonArgs.OptionalString(spec, "label");
                if (label != null)
                {
                    if (references.Count != 2)
                    {
                        throw new ArgumentException("label можно назначить только размеру между двумя ссылками");
                    }

                    var parameter = FamilyParameterSpecs.Find(manager, label);
                    try
                    {
                        dimension.FamilyLabel = parameter;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Не удалось назначить параметр '{label}' размеру между '{references[0].Description}' и '{references[1].Description}': {ex.Message}. Параметр должен быть типа Length.",
                            ex);
                    }
                }

                if (JsonArgs.OptionalBool(spec, "locked", false))
                {
                    dimension.IsLocked = true;
                }

                doc.Regenerate();
                created.Add(FamilyConstraints.Describe(doc, dimension));
            }

            return created;
        });
    }
}
