using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Utils.ProjectModel;

/// <summary>Помощники команд модели проекта: единицы, поиск типов, запись параметров.</summary>
public static class ProjectModelUtils
{
    public const double MmPerFoot = 304.8;

    public static double Mm(double feet) => Math.Round(feet * MmPerFoot, 2);

    public static double[] Mm(XYZ p) => new[] { Mm(p.X), Mm(p.Y), Mm(p.Z) };

    /// <summary>Точка плана [x, y] или {x, y} в мм → футы (Z = 0).</summary>
    public static XYZ PlanPointMm(JToken token, string what)
    {
        double x, y;
        if (token is JArray array && array.Count >= 2)
        {
            x = array[0].Value<double>();
            y = array[1].Value<double>();
        }
        else if (token is JObject obj && obj["x"] != null && obj["y"] != null)
        {
            x = obj["x"].Value<double>();
            y = obj["y"].Value<double>();
        }
        else
        {
            throw new ArgumentException($"{what}: ожидается [x, y] или {{x, y}} в мм");
        }

        return new XYZ(x / MmPerFoot, y / MmPerFoot, 0);
    }

    public static Level FindLevel(Document doc, string name)
    {
        var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
        return levels.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"Уровень '{name}' не найден. Уровни: {string.Join(", ", levels.Select(l => l.Name))}");
    }

    /// <summary>Имя типа для поиска: «Тип» или «Семейство : Тип».</summary>
    public static bool NameMatches(ElementType type, string name)
    {
        if (type == null || string.IsNullOrWhiteSpace(name)) return false;
        name = name.Trim();
        if (string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
        var colon = name.IndexOf(':');
        if (colon <= 0) return false;
        var family = name.Substring(0, colon).Trim();
        var typeName = name.Substring(colon + 1).Trim();
        return string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase)
               && string.Equals(type.FamilyName, family, StringComparison.OrdinalIgnoreCase);
    }

    public static string FullName(ElementType type) =>
        string.IsNullOrEmpty(type.FamilyName) ? type.Name : $"{type.FamilyName} : {type.Name}";

    /// <summary>Типы по виду: wall, curtainWall, mullion, panel, profile или категория OST_….</summary>
    public static List<ElementType> TypesOfKind(Document doc, string kind)
    {
        var all = new FilteredElementCollector(doc).WhereElementIsElementType().Cast<Element>().OfType<ElementType>();
        switch ((kind ?? "").Trim().ToLowerInvariant())
        {
            case "":
            case "any":
                return all.ToList();
            case "wall":
                return all.OfType<WallType>().Where(t => t.Kind != WallKind.Curtain).Cast<ElementType>().ToList();
            case "walltype":
                return all.OfType<WallType>().Cast<ElementType>().ToList();
            case "curtainwall":
            case "curtain_wall":
                return all.OfType<WallType>().Where(t => t.Kind == WallKind.Curtain).Cast<ElementType>().ToList();
            case "mullion":
                return all.OfType<MullionType>().Cast<ElementType>().ToList();
            case "panel":
                return all.Where(t => t is PanelType || t.Category?.Id.GetValue() == (long)BuiltInCategory.OST_CurtainWallPanels).ToList();
            case "profile":
                return all.Where(t => t.Category?.Id.GetValue() == (long)BuiltInCategory.OST_ProfileFamilies).ToList();
            default:
                if (Enum.TryParse(kind, true, out BuiltInCategory category))
                {
                    return all.Where(t => t.Category?.Id.GetValue() == (long)category).ToList();
                }

                throw new ArgumentException($"Неизвестный kind '{kind}'. Допустимо: wall, curtainWall, mullion, panel, profile, any или категория OST_…");
        }
    }

    public static ElementType FindType(Document doc, string name, string kind)
    {
        var candidates = TypesOfKind(doc, kind);
        var found = candidates.Where(t => NameMatches(t, name)).ToList();
        if (found.Count == 1) return found[0];
        if (found.Count > 1)
        {
            throw new ArgumentException($"Имя '{name}' неоднозначно: {string.Join("; ", found.Select(t => $"{FullName(t)} ({t.GetType().Name})"))}. Укажите kind или «Семейство : Тип»");
        }

        var sample = candidates.Take(40).Select(FullName);
        throw new ArgumentException($"Тип '{name}' не найден (kind: {kind ?? "any"}). Есть: {string.Join("; ", sample)}{(candidates.Count > 40 ? "; …" : "")}");
    }

    /// <summary>Параметр по имени BuiltInParameter (SPACING_LENGTH_VERT) или по отображаемому имени.</summary>
    public static Parameter FindParameter(Element element, string name)
    {
        if (Enum.TryParse(name, true, out BuiltInParameter builtIn) && !int.TryParse(name, out _))
        {
            var p = element.get_Parameter(builtIn);
            if (p != null) return p;
        }

        return element.Parameters.Cast<Parameter>()
            .FirstOrDefault(p => string.Equals(p.Definition?.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsLength(Parameter p)
    {
#if REVIT2022_OR_GREATER
        return p.Definition.GetDataType() == SpecTypeId.Length;
#else
        return p.Definition.ParameterType == ParameterType.Length;
#endif
    }

    public static bool IsAngle(Parameter p)
    {
#if REVIT2022_OR_GREATER
        return p.Definition.GetDataType() == SpecTypeId.Angle;
#else
        return p.Definition.ParameterType == ParameterType.Angle;
#endif
    }

    public static string BuiltInName(Parameter p)
    {
        if (p.Definition is InternalDefinition internalDefinition && internalDefinition.BuiltInParameter != BuiltInParameter.INVALID)
        {
            return internalDefinition.BuiltInParameter.ToString();
        }

        return null;
    }

    public static object Describe(Document doc, Parameter p)
    {
        object value;
        switch (p.StorageType)
        {
            case StorageType.Double:
                value = IsLength(p) ? Mm(p.AsDouble()) : IsAngle(p) ? Math.Round(p.AsDouble() * 180 / Math.PI, 4) : p.AsDouble();
                break;
            case StorageType.Integer:
                value = p.AsInteger();
                break;
            case StorageType.ElementId:
                var target = doc.GetElement(p.AsElementId());
                value = target == null ? null : target is ElementType t ? FullName(t) : target.Name;
                break;
            default:
                value = p.AsString();
                break;
        }

        return new
        {
            name = p.Definition?.Name,
            builtIn = BuiltInName(p),
            storage = p.StorageType.ToString(),
            unit = p.StorageType == StorageType.Double ? (IsLength(p) ? "mm" : IsAngle(p) ? "deg" : null) : null,
            value,
            display = p.AsValueString(),
            readOnly = p.IsReadOnly
        };
    }

    /// <summary>
    /// Записывает значение: длины — мм, углы — градусы, ссылки — имя типа или материала,
    /// целые списки — по отображаемому тексту («Фиксированное расстояние»), строка с единицами — через SetValueString.
    /// </summary>
    public static void SetParameter(Document doc, Element element, string name, JToken value)
    {
        var p = FindParameter(element, name)
                ?? throw new ArgumentException($"У '{element.Name}' нет параметра '{name}'");
        if (p.IsReadOnly) throw new ArgumentException($"Параметр '{p.Definition.Name}' только для чтения");

        bool ok;
        switch (p.StorageType)
        {
            case StorageType.Double:
                if (value.Type == JTokenType.String)
                {
                    ok = p.SetValueString(value.Value<string>());
                }
                else
                {
                    var number = value.Value<double>();
                    ok = p.Set(IsLength(p) ? number / MmPerFoot : IsAngle(p) ? number * Math.PI / 180 : number);
                }

                break;
            case StorageType.Integer:
                ok = SetInteger(p, value);
                break;
            case StorageType.ElementId:
                ok = SetElementId(doc, p, value);
                break;
            default:
                ok = p.Set(value.Type == JTokenType.Null ? string.Empty : value.ToString());
                break;
        }

        if (!ok) throw new ArgumentException($"Revit не принял значение '{value}' для '{p.Definition.Name}'");
    }

    private static bool SetInteger(Parameter p, JToken value)
    {
        if (value.Type == JTokenType.Boolean) return p.Set(value.Value<bool>() ? 1 : 0);
        if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) return p.Set((int)Math.Round(value.Value<double>()));

        var text = value.Value<string>()?.Trim() ?? string.Empty;
        if (int.TryParse(text, out var parsed)) return p.Set(parsed);
        var lowered = text.ToLowerInvariant();
        if (lowered == "true" || lowered == "да" || lowered == "yes") return p.Set(1);
        if (lowered == "false" || lowered == "нет" || lowered == "no") return p.Set(0);

        // Списки (компоновка сетки, условие соединения): перебор значений и сравнение с текстом в интерфейсе.
        var original = p.AsInteger();
        var seen = new List<string>();
        for (var candidate = -1; candidate <= 32; candidate++)
        {
            try
            {
                if (!p.Set(candidate)) continue;
            }
            catch (Exception)
            {
                continue;
            }

            var shown = p.AsValueString();
            if (string.Equals(shown?.Trim(), text, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(shown) && !seen.Contains(shown)) seen.Add(shown);
        }

        p.Set(original);
        throw new ArgumentException($"Значение '{text}' не подходит для '{p.Definition.Name}'. Варианты: {string.Join("; ", seen)}");
    }

    private static bool SetElementId(Document doc, Parameter p, JToken value)
    {
        if (value.Type == JTokenType.Null) return p.Set(ElementId.InvalidElementId);
        if (value.Type == JTokenType.Integer) return p.Set(FamilyEditorIds.FromNumber(value.Value<long>()));

        var text = value.Value<string>()?.Trim() ?? string.Empty;
        var lowered = text.ToLowerInvariant();
        if (lowered == "" || lowered == "none" || lowered == "нет" || lowered == "<нет>" || lowered == "<none>")
        {
            return p.Set(ElementId.InvalidElementId);
        }

        var candidates = new List<Element>();
        candidates.AddRange(new FilteredElementCollector(doc).WhereElementIsElementType().Cast<Element>().OfType<ElementType>().Where(t => NameMatches(t, text)));
        candidates.AddRange(new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Element>().Where(m => string.Equals(m.Name, text, StringComparison.OrdinalIgnoreCase)));
        if (candidates.Count == 0) throw new ArgumentException($"Элемент '{text}' для '{p.Definition.Name}' не найден (тип или материал)");

        // Одно имя может быть у разных классов (панель и профиль): подходит тот, который Revit примет.
        foreach (var candidate in candidates)
        {
            try
            {
                if (p.Set(candidate.Id) && p.AsElementId() == candidate.Id) return true;
            }
            catch (Exception)
            {
            }
        }

        return false;
    }

    public static WallKind? CurtainKind(Wall wall) => (wall.Document.GetElement(wall.GetTypeId()) as WallType)?.Kind;

    /// <summary>Стены по id, по имени типа или все витражи проекта.</summary>
    public static List<Wall> ResolveWalls(Document doc, JObject args, string idsKey = "ids", string typeKey = "wallType")
    {
        if (JsonArgs.Has(args, idsKey))
        {
            return JsonArgs.Ids(args, idsKey).Select(id => doc.GetElement(FamilyEditorIds.FromNumber(id)) as Wall
                                                         ?? throw new ArgumentException($"Элемент {id} не стена")).ToList();
        }

        var walls = new FilteredElementCollector(doc).OfClass(typeof(Wall)).Cast<Wall>().Where(w => w.CurtainGrid != null).ToList();
        var typeName = JsonArgs.OptionalString(args, typeKey);
        if (typeName != null)
        {
            walls = walls.Where(w => doc.GetElement(w.GetTypeId()) is WallType t && NameMatches(t, typeName)).ToList();
            if (walls.Count == 0) throw new ArgumentException($"Витражей типа '{typeName}' нет");
        }

        if (walls.Count == 0) throw new InvalidOperationException("В проекте нет витражей");
        return walls;
    }
}
