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
            var trims = new List<Trim>();
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
                var depth = JsonArgs.Has(spec, "depth") ? JsonArgs.OptionalDouble(spec, "depth", DefaultExtentMm) / 304.8 : extent;
                trims.Add(new Trim { Plane = plane, Name = name, Origin = origin, Along = along, Extent = extent, Depth = depth });
            }

            doc.Regenerate();
            // По умолчанию Revit тянет плоскость в перпендикулярных видах на 9 м (30 футов): обрезаем её модельные
            // границы во всех видах, где она видна. Вдоль линии на виде создания — extent, поперёк — depth.
            foreach (var trim in trims)
            {
                TrimExtents(doc, trim, notes);
            }

            doc.Regenerate();
            return new
            {
                planes = created.Select(FamilyDocInfo.DescribeReferencePlane).ToList(),
                notes
            };
        });
    }

    private sealed class Trim
    {
        public ReferencePlane Plane;
        public string Name;
        public XYZ Origin;
        public XYZ Along;
        public double Extent;
        public double Depth;
    }

    /// <summary>Модельные границы плоскости во всех планах и фасадах: отрезок с центром в проекции origin.</summary>
    private static void TrimExtents(Document doc, Trim trim, List<string> notes)
    {
        var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && (v is ViewPlan || v.ViewType == ViewType.Elevation || v.ViewType == ViewType.Section));
        foreach (var view in views)
        {
            try
            {
                if (!trim.Plane.CanBeVisibleInView(view)) continue;
                var curves = trim.Plane.GetCurvesInView(DatumExtentType.Model, view);
                if (curves.Count == 0) continue;
                var p0 = curves[0].GetEndPoint(0);
                var direction = (curves[0].GetEndPoint(1) - p0).Normalize();
                var center = p0 + direction * (trim.Origin - p0).DotProduct(direction);
                var half = Math.Abs(direction.DotProduct(trim.Along)) > 0.99 ? trim.Extent : trim.Depth;
                trim.Plane.SetCurveInView(DatumExtentType.Model, view, Line.CreateBound(center - direction * half, center + direction * half));
            }
            catch (Exception ex)
            {
                notes.Add($"'{trim.Name}': границы в виде «{view.Name}» не изменены ({ex.Message})");
            }
        }
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
