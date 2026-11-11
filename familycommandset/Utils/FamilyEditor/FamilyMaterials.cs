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

    public static object Describe(Material material)
    {
        var color = material.Color;
        return new
        {
            id = material.Id.GetValue(),
            name = material.Name,
            materialClass = material.MaterialClass,
            color = color != null && color.IsValid ? new[] { (int)color.Red, color.Green, color.Blue } : null,
            transparency = material.Transparency
        };
    }

    private static byte Channel(JToken value)
    {
        var number = value.Value<int>();
        if (number < 0 || number > 255) throw new ArgumentException("Составляющие цвета должны быть от 0 до 255");
        return (byte)number;
    }
}
