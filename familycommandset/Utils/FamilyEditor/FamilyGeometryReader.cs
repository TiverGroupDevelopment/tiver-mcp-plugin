using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

/// <summary>
/// Грани форм с устойчивыми индексами и ссылками (Reference) для размеров и выравнивания,
/// а также подробное описание формы: объём, грани, эскиз.
/// </summary>
public static class FamilyGeometryReader
{
    private const double ParallelTolerance = 1e-6;

    /// <summary>Грани элемента в порядке обхода геометрии. Индекс грани устойчив, пока геометрия не менялась.</summary>
    public static List<Face> Faces(Element element)
    {
        var faces = new List<Face>();
        var geometry = element.get_Geometry(new Options { ComputeReferences = true, IncludeNonVisibleObjects = true });
        if (geometry == null) return faces;
        Collect(geometry, faces);
        return faces;
    }

    public static double VolumeCm3(Element element)
    {
        var geometry = element.get_Geometry(new Options { IncludeNonVisibleObjects = true });
        if (geometry == null) return 0;
        var volume = 0.0;
        foreach (var solid in Solids(geometry)) volume += solid.Volume;
        return Math.Round(volume * 28316.846592, 3);
    }

    /// <summary>Грани элемента с индексами — для ссылок в размерах, выравнивании и коннекторах.</summary>
    public static List<object> DescribeFaces(Element element)
    {
        return Faces(element).Select((face, index) => DescribeFace(face, index)).ToList();
    }

    public static object DescribeDetailed(Document doc, FamilyManager manager, GenericForm form)
    {
        var faces = Faces(form);
        return new
        {
            summary = FamilyFormUtils.Describe(doc, manager, form),
            volumeCm3 = VolumeCm3(form),
            shape = DescribeShape(form),
            faces = faces.Select((face, index) => DescribeFace(face, index)).ToList()
        };
    }

    /// <summary>
    /// Разрешает спецификацию ссылки: имя опорной плоскости (строка), {referencePlane}, {referencePlaneId},
    /// {level} или {elementId, faceIndex}.
    /// </summary>
    public static ResolvedReference Resolve(Document doc, JToken spec)
    {
        if (spec == null || spec.Type == JTokenType.Null) throw new ArgumentException("Ссылка не задана");

        if (spec.Type == JTokenType.String)
        {
            return FromReferencePlane(FamilyDocInfo.FindReferencePlane(doc, spec.Value<string>()));
        }

        if (!(spec is JObject obj))
        {
            throw new ArgumentException($"Ссылка должна быть именем опорной плоскости или объектом, получено: {spec}");
        }

        if (JsonArgs.Has(obj, "referencePlane"))
        {
            return FromReferencePlane(FamilyDocInfo.FindReferencePlane(doc, obj["referencePlane"].Value<string>()));
        }

        if (JsonArgs.Has(obj, "referencePlaneId"))
        {
            var id = obj["referencePlaneId"].Value<long>();
            var plane = doc.GetElement(FamilyEditorIds.FromNumber(id)) as ReferencePlane
                        ?? throw new ArgumentException($"Элемент {id} не является опорной плоскостью");
            return FromReferencePlane(plane);
        }

        if (JsonArgs.Has(obj, "level"))
        {
            var name = obj["level"].Value<string>();
            var level = new FilteredElementCollector(doc)
                            .OfClass(typeof(Level))
                            .Cast<Level>()
                            .FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ArgumentException($"Уровень '{name}' не найден в семействе");
            return new ResolvedReference(level.GetPlaneReference(), XYZ.BasisZ, new XYZ(0, 0, level.ProjectElevation), $"level:{level.Name}");
        }

        if (JsonArgs.Has(obj, "elementId"))
        {
            var id = obj["elementId"].Value<long>();
            if (!JsonArgs.Has(obj, "faceIndex"))
            {
                throw new ArgumentException("Для ссылки на форму укажите faceIndex (см. get_family_geometry с detail: true → faces[].index)");
            }

            var index = obj["faceIndex"].Value<int>();
            var element = doc.GetElement(FamilyEditorIds.FromNumber(id))
                          ?? throw new ArgumentException($"Элемент {id} не найден");
            var faces = Faces(element);
            if (index < 0 || index >= faces.Count)
            {
                throw new ArgumentException($"У элемента {id} нет грани с индексом {index} (всего граней: {faces.Count})");
            }

            var planar = faces[index] as PlanarFace
                         ?? throw new ArgumentException($"Грань {index} элемента {id} не плоская ({faces[index].GetType().Name}); для размеров и выравнивания нужны плоские грани");
            var reference = planar.Reference
                            ?? throw new InvalidOperationException($"Revit не выдал ссылку для грани {index} элемента {id}");
            return new ResolvedReference(reference, planar.FaceNormal.Normalize(), planar.Origin, $"element:{id}/face:{index}");
        }

        throw new ArgumentException("Ссылка должна содержать referencePlane, referencePlaneId, level или elementId + faceIndex");
    }

    public static bool AreParallel(XYZ a, XYZ b)
    {
        return a.Normalize().CrossProduct(b.Normalize()).GetLength() < ParallelTolerance;
    }

    private static ResolvedReference FromReferencePlane(ReferencePlane plane)
    {
        var geometry = plane.GetPlane();
        var label = string.IsNullOrEmpty(plane.Name) ? $"referencePlane:#{plane.Id.GetValue()}" : $"referencePlane:{plane.Name}";
        return new ResolvedReference(plane.GetReference(), geometry.Normal.Normalize(), geometry.Origin, label);
    }

    private static object DescribeFace(Face face, int index)
    {
        if (face is PlanarFace planar)
        {
            return new
            {
                index,
                type = "Planar",
                normal = FamilyDocInfo.Unit(planar.FaceNormal),
                originMm = FamilyDocInfo.Mm(planar.Origin),
                areaCm2 = Math.Round(face.Area * 929.0304, 3),
                canReference = planar.Reference != null
            };
        }

        return new
        {
            index,
            type = face.GetType().Name.Replace("Face", string.Empty),
            normal = (double[])null,
            originMm = (double[])null,
            areaCm2 = Math.Round(face.Area * 929.0304, 3),
            canReference = false
        };
    }

    private static object DescribeShape(GenericForm form)
    {
        switch (form)
        {
            case Extrusion extrusion:
                return new
                {
                    startMm = FamilyDocInfo.Mm(extrusion.StartOffset),
                    endMm = FamilyDocInfo.Mm(extrusion.EndOffset),
                    sketchPlane = DescribeSketchPlane(extrusion.Sketch),
                    profile = DescribeProfile(extrusion.Sketch?.Profile)
                };
            case Blend blend:
                return new
                {
                    bottomMm = FamilyDocInfo.Mm(blend.BottomOffset),
                    topMm = FamilyDocInfo.Mm(blend.TopOffset),
                    bottomProfile = DescribeProfile(blend.BottomProfile),
                    topProfile = DescribeProfile(blend.TopProfile)
                };
            case Revolution revolution:
                return new
                {
                    startAngleDeg = Math.Round(revolution.StartAngle * 180.0 / Math.PI, 4),
                    endAngleDeg = Math.Round(revolution.EndAngle * 180.0 / Math.PI, 4),
                    sketchPlane = DescribeSketchPlane(revolution.Sketch),
                    profile = DescribeProfile(revolution.Sketch?.Profile)
                };
            default:
                return null;
        }
    }

    private static object DescribeSketchPlane(Sketch sketch)
    {
        var plane = sketch?.SketchPlane?.GetPlane();
        if (plane == null) return null;
        return new
        {
            name = sketch.SketchPlane.Name,
            originMm = FamilyDocInfo.Mm(plane.Origin),
            normal = FamilyDocInfo.Unit(plane.Normal)
        };
    }

    private static object DescribeProfile(CurveArrArray profile)
    {
        if (profile == null) return null;
        var loops = new List<object>();
        foreach (CurveArray loop in profile)
        {
            var curves = new List<object>();
            foreach (Curve curve in loop)
            {
                if (!curve.IsBound)
                {
                    curves.Add(new { type = curve.GetType().Name, startMm = (double[])null, endMm = (double[])null });
                    continue;
                }

                curves.Add(new
                {
                    type = curve.GetType().Name,
                    startMm = FamilyDocInfo.Mm(curve.GetEndPoint(0)),
                    endMm = FamilyDocInfo.Mm(curve.GetEndPoint(1))
                });
            }

            loops.Add(curves);
        }

        return loops;
    }

    private static void Collect(GeometryElement geometry, List<Face> faces)
    {
        foreach (var solid in Solids(geometry))
        {
            foreach (Face face in solid.Faces) faces.Add(face);
        }
    }

    private static IEnumerable<Solid> Solids(GeometryElement geometry)
    {
        foreach (var item in geometry)
        {
            if (item is Solid solid && solid.Faces.Size > 0)
            {
                yield return solid;
            }
            else if (item is GeometryInstance instance)
            {
                foreach (var nested in Solids(instance.GetInstanceGeometry())) yield return nested;
            }
        }
    }
}

public sealed class ResolvedReference
{
    public ResolvedReference(Reference reference, XYZ normal, XYZ point, string description)
    {
        Reference = reference;
        Normal = normal;
        Point = point;
        Description = description;
    }

    public Reference Reference { get; }

    /// <summary>Единичная нормаль плоскости ссылки.</summary>
    public XYZ Normal { get; }

    /// <summary>Точка на плоскости ссылки, футы.</summary>
    public XYZ Point { get; }

    public string Description { get; }
}
