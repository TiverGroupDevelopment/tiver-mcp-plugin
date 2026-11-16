using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

public static class FamilyMaterials
{
    /// <summary>Цвет из [r, g, b], {r, g, b} или строки "#RRGGBB".</summary>
    public static Color ParseColor(JToken token)
    {
        if (token.Type == JTokenType.String)
        {
            var text = token.Value<string>().Trim().TrimStart('#');
            if (text.Length == 6 && int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            {
                return new Color((byte)(rgb >> 16 & 0xFF), (byte)(rgb >> 8 & 0xFF), (byte)(rgb & 0xFF));
            }

            throw new ArgumentException($"Цвет '{token}' должен быть в формате #RRGGBB");
        }

        if (token is JArray array && array.Count >= 3)
        {
            return new Color(Channel(array[0]), Channel(array[1]), Channel(array[2]));
        }

        if (token is JObject obj && obj["r"] != null && obj["g"] != null && obj["b"] != null)
        {
            return new Color(Channel(obj["r"]), Channel(obj["g"]), Channel(obj["b"]));
        }

        throw new ArgumentException($"Цвет должен быть [r, g, b], {{r, g, b}} или \"#RRGGBB\", получено: {token}");
    }

    private const double CubicFeetToCubicMeters = 0.028316846592;

    public static object Describe(Material material)
    {
        var color = material.Color;
        return new
        {
            id = material.Id.GetValue(),
            name = material.Name,
            materialClass = material.MaterialClass,
            color = color != null && color.IsValid ? new[] { (int)color.Red, color.Green, color.Blue } : null,
            transparency = material.Transparency,
            densityKgM3 = Density(material)
        };
    }

    /// <summary>Плотность материала из физического ресурса, кг/м³; null — ресурса нет.</summary>
    public static double? Density(Material material)
    {
        var id = material.StructuralAssetId;
        if (id == null || id == ElementId.InvalidElementId) return null;
        var asset = (material.Document.GetElement(id) as PropertySetElement)?.GetStructuralAsset();
        return asset == null || asset.Density <= 0 ? (double?)null : Math.Round(asset.Density / CubicFeetToCubicMeters, 3);
    }

    /// <summary>
    /// Задаёт плотность через собственный физический ресурс материала (создаётся, если ресурса нет или он общий
    /// с другими материалами). Нужна get_family_mass.
    /// </summary>
    public static void SetDensity(Document doc, Material material, double densityKgM3)
    {
        if (densityKgM3 <= 0) throw new ArgumentException("densityKgM3 должна быть больше 0");
        // Имена ресурсов в документе уникальны: добавляем короткий суффикс.
        var asset = new StructuralAsset($"{material.Name} MCP {Guid.NewGuid().ToString("N").Substring(0, 6)}", StructuralAssetClass.Basic)
        {
            Density = densityKgM3 * CubicFeetToCubicMeters
        };
        var property = PropertySetElement.Create(doc, asset);
        material.SetMaterialAspectByPropertySet(MaterialAspect.Structural, property.Id);
    }

    private static byte Channel(JToken value)
    {
        var number = value.Value<int>();
        if (number < 0 || number > 255) throw new ArgumentException("Составляющие цвета должны быть от 0 до 255");
        return (byte)number;
    }
}
