using Newtonsoft.Json.Linq;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

public static class JsonArgs
{
    public static bool Has(JObject args, string key)
    {
        var token = args[key];
        return token != null && token.Type != JTokenType.Null && token.Type != JTokenType.Undefined;
    }

    public static JToken Required(JObject args, string key)
    {
        return Has(args, key) ? args[key] : throw new ArgumentException($"Не задан обязательный параметр '{key}'");
    }

    public static string OptionalString(JObject args, string key)
    {
        if (!Has(args, key)) return null;
        var value = args[key].Value<string>()?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static string RequiredString(JObject args, string key)
    {
        return OptionalString(args, key) ?? throw new ArgumentException($"Не задан обязательный параметр '{key}'");
    }

    public static double OptionalDouble(JObject args, string key, double fallback)
    {
        return Has(args, key) ? args[key].Value<double>() : fallback;
    }

    public static double RequiredDouble(JObject args, string key)
    {
        return Required(args, key).Value<double>();
    }

    public static bool OptionalBool(JObject args, string key, bool fallback)
    {
        return Has(args, key) ? args[key].Value<bool>() : fallback;
    }

    public static JObject RequiredObject(JObject args, string key)
    {
        return Required(args, key) as JObject ?? throw new ArgumentException($"Параметр '{key}' должен быть объектом");
    }

    public static JArray RequiredArray(JObject args, string key)
    {
        return Required(args, key) as JArray ?? throw new ArgumentException($"Параметр '{key}' должен быть массивом");
    }

    /// <summary>Три числа из [x, y, z] или {x, y, z} без пересчёта единиц.</summary>
    public static Autodesk.Revit.DB.XYZ Vector(JToken token, string what)
    {
        if (token is JArray array && array.Count >= 3)
        {
            return new Autodesk.Revit.DB.XYZ(array[0].Value<double>(), array[1].Value<double>(), array[2].Value<double>());
        }

        if (token is JObject obj && obj["x"] != null && obj["y"] != null && obj["z"] != null)
        {
            return new Autodesk.Revit.DB.XYZ(obj["x"].Value<double>(), obj["y"].Value<double>(), obj["z"].Value<double>());
        }

        throw new ArgumentException($"{what} должен быть [x, y, z] или {{\"x\":..,\"y\":..,\"z\":..}}, получено: {token}");
    }

    /// <summary>Точка в мм, пересчитанная во внутренние футы.</summary>
    public static Autodesk.Revit.DB.XYZ PointMm(JToken token, string what)
    {
        return Vector(token, what) / 304.8;
    }

    public static List<long> Ids(JObject args, string key)
    {
        var ids = RequiredArray(args, key).Select(t => t.Value<long>()).Distinct().ToList();
        if (ids.Count == 0) throw new ArgumentException($"'{key}' пуст");
        return ids;
    }
}
