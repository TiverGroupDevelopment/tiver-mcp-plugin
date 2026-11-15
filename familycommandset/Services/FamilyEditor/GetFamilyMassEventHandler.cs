using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Масса семейства: объём каждой твёрдой формы × плотность её материала (физический ресурс материала)
/// или densityKgM3. Можно записать результат в числовой параметр семейства (writeParameter) в граммах или килограммах.
/// </summary>
public class GetFamilyMassEventHandler : FamilyEditorEventHandlerBase
{
    private const double CubicFeetToCubicMeters = 0.028316846592;

    public override string GetName() => "Get family mass";

    protected override object Run(UIApplication app, JObject args)
    {
        var overrideDensity = JsonArgs.Has(args, "densityKgM3") ? args["densityKgM3"].Value<double>() : (double?)null;
        var writeParameter = JsonArgs.OptionalString(args, "writeParameter");
        var unit = (JsonArgs.OptionalString(args, "unit") ?? "g").ToLowerInvariant();
        if (unit != "g" && unit != "kg") throw new ArgumentException("unit: допустимо g или kg");
        var allTypes = JsonArgs.OptionalBool(args, "allTypes", false);

        if (writeParameter == null)
        {
            return Read(app, args, session => ToReport(Compute(session.Document, overrideDensity)));
        }

        return Edit(app, args, "MCP: масса семейства", doc =>
        {
            var manager = doc.FamilyManager;
            var parameter = FamilyParameterSpecs.Find(manager, writeParameter);
            var original = manager.CurrentType;
            var types = allTypes ? FamilyParameterSpecs.AllTypes(manager) : new List<FamilyType> { original };
            var perType = new List<object>();
            foreach (var type in types)
            {
                if (type == null) continue;
                manager.CurrentType = type;
                doc.Regenerate();
                var mass = Compute(doc, overrideDensity);
                var value = unit == "kg" ? mass.TotalKg : mass.TotalKg * 1000;
                FamilyParameterSpecs.SetCurrentTypeValue(doc, manager, parameter, new JValue(Math.Round(value, 3)));
                perType.Add(new { type = type.Name, mass = ToReport(mass), written = Math.Round(value, 3), unit });
            }

            if (original != null) manager.CurrentType = original;
            return new { parameter = writeParameter, types = perType };
        });
    }

    private sealed class MassResult
    {
        public double TotalKg { get; set; }
        public List<object> Forms { get; } = new List<object>();
        public List<string> Problems { get; } = new List<string>();
    }

    private static object ToReport(MassResult result) => new
    {
        totalKg = Math.Round(result.TotalKg, 6),
        totalG = Math.Round(result.TotalKg * 1000, 3),
        forms = result.Forms,
        problems = result.Problems
    };

    private static MassResult Compute(Document doc, double? overrideDensity)
    {
        var result = new MassResult();
        foreach (var form in new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements().OfType<GenericForm>())
        {
            if (!form.IsSolid) continue;
            var volumeM3 = FamilyGeometryReader.VolumeCm3(form) / 1e6;
            var materialId = form.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM)?.AsElementId();
            var material = materialId == null ? null : doc.GetElement(materialId) as Material;
            var density = overrideDensity ?? Density(doc, material);
            if (density == null)
            {
                result.Problems.Add($"Форма {form.Id.GetValue()}: у материала «{material?.Name ?? "<По категории>"}» нет плотности — передайте densityKgM3 или задайте физический ресурс материала");
            }

            var massKg = density.HasValue ? volumeM3 * density.Value : 0;
            result.TotalKg += massKg;
            result.Forms.Add(new
            {
                id = form.Id.GetValue(),
                kind = form.GetType().Name,
                material = material?.Name,
                volumeCm3 = Math.Round(volumeM3 * 1e6, 3),
                densityKgM3 = density.HasValue ? Math.Round(density.Value, 1) : (double?)null,
                massG = Math.Round(massKg * 1000, 3)
            });
        }

        return result;
    }

    private static double? Density(Document doc, Material material)
    {
        if (material == null) return null;
        var id = material.StructuralAssetId;
        if (id == null || id == ElementId.InvalidElementId) return null;
        if (!(doc.GetElement(id) is PropertySetElement property)) return null;
        var asset = property.GetStructuralAsset();
        if (asset == null || asset.Density <= 0) return null;
        // Плотность во внутренних единицах — кг/фут³.
        return asset.Density / CubicFeetToCubicMeters;
    }
}
