using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class CreateFamilyReferencePlanesEventHandler : FamilyEditorEventHandlerBase
{
    private const double DefaultExtentMm = 1000;

    public override string GetName() => "Create family reference planes";

    protected override object Run(UIApplication app, JObject args)
    {
        var specs = JsonArgs.RequiredArray(args, "planes").OfType<JObject>().ToList();
        if (specs.Count == 0) throw new ArgumentException("planes пуст");

        return Edit(app, args, "MCP: create reference planes", doc =>
        {
            var existingNames = new HashSet<string>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ReferencePlane))
                    .Cast<ReferencePlane>()
                    .Select(r => r.Name)
                    .Where(n => !string.IsNullOrEmpty(n)),
                StringComparer.OrdinalIgnoreCase);

            var created = new List<ReferencePlane>();
            var notes = new List<string>();

            foreach (var spec in specs)
            {
                var name = JsonArgs.RequiredString(spec, "name");
                if (!existingNames.Add(name))
                {
                    throw new ArgumentException($"Опорная плоскость с именем '{name}' уже существует");
                }

                ResolveGeometry(spec, out var origin, out var normal);
                var view = FamilyDocInfo.PickViewForNormal(doc, normal, JsonArgs.OptionalString(spec, "view"));
                var viewDirection = view.ViewDirection.Normalize();
                var along = normal.CrossProduct(viewDirection).Normalize();
                var extent = JsonArgs.OptionalDouble(spec, "extent", DefaultExtentMm) / 304.8;
                if (extent <= doc.Application.ShortCurveTolerance) throw new ArgumentException("extent слишком мал");

                var plane = doc.FamilyCreate.NewReferencePlane(origin + along * extent, origin - along * extent, viewDirection, view)
                            ?? throw new InvalidOperationException($"Revit не создал опорную плоскость '{name}'");
                plane.Name = name;

                var isReference = JsonArgs.OptionalString(spec, "isReference");
                if (isReference != null)
                {
                    var parameter = plane.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME);
                    if (parameter == null || parameter.IsReadOnly)
                    {
                        notes.Add($"'{name}': параметр «Является опорной» недоступен, isReference не задан");
                    }
                    else
                    {
                        parameter.Set(FamilyDocInfo.ParseReferenceType(isReference));
                    }
                }

                if (JsonArgs.Has(spec, "definesOrigin"))
                {
                    var parameter = plane.get_Parameter(BuiltInParameter.DATUM_PLANE_DEFINES_ORIGIN);
                    if (parameter == null || parameter.IsReadOnly)
                    {
                        notes.Add($"'{name}': параметр «Определяет начало» недоступен");
                    }
                    else
                    {
                        parameter.Set(JsonArgs.OptionalBool(spec, "definesOrigin", false) ? 1 : 0);
                    }
                }

                created.Add(plane);
            }

            doc.Regenerate();
            return new
            {
                planes = created.Select(FamilyDocInfo.DescribeReferencePlane).ToList(),
                notes
            };
        });
    }

    private static void ResolveGeometry(JObject spec, out XYZ origin, out XYZ normal)
    {
        var axis = JsonArgs.OptionalString(spec, "axis");
        if (axis != null)
        {
            switch (axis.ToUpperInvariant())
            {
                case "X":
                    normal = XYZ.BasisX;
                    break;
                case "Y":
                    normal = XYZ.BasisY;
                    break;
                case "Z":
                    normal = XYZ.BasisZ;
                    break;
                default:
                    throw new ArgumentException($"Неизвестная ось '{axis}'. Допустимо: X, Y, Z");
            }

            origin = normal * (JsonArgs.OptionalDouble(spec, "offset", 0) / 304.8);
            return;
        }

        if (!JsonArgs.Has(spec, "normal"))
        {
            throw new ArgumentException("Задайте плоскость через axis + offset либо через origin + normal");
        }

        normal = JsonArgs.Vector(spec["normal"], "normal");
        if (normal.GetLength() < 1e-9) throw new ArgumentException("normal не может быть нулевым вектором");
        normal = normal.Normalize();
        origin = JsonArgs.Has(spec, "origin") ? JsonArgs.PointMm(spec["origin"], "origin") : XYZ.Zero;
    }
}
