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
}
