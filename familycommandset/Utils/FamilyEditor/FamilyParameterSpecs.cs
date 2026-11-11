using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

public enum FamilyValueKind
{
    Length,
    Angle,
    Area,
    Volume,
    Number,
    Integer,
    YesNo,
    Text,
    Material,
    Other
}

public static class FamilyParameterSpecs
{
    private const double MillimetersPerFoot = 304.8;
    private const double SquareMetersPerSquareFoot = 0.09290304;
    private const double CubicMetersPerCubicFoot = 0.028316846592;

    public static readonly string[] GroupNames =
    {
        "Dimensions", "Constraints", "Data", "Text", "IdentityData", "Materials", "Graphics", "Construction", "Visibility", "Other"
    };

    public static FamilyValueKind ParseKind(string value)
    {
        if (Enum.TryParse(value, true, out FamilyValueKind kind) && kind != FamilyValueKind.Other) return kind;
        throw new ArgumentException(
            $"Неизвестный тип параметра '{value}'. Допустимо: Length, Angle, Area, Volume, Number, Integer, YesNo, Text, Material");
    }

    public static string DefaultGroup(FamilyValueKind kind)
    {
        switch (kind)
        {
            case FamilyValueKind.Length:
            case FamilyValueKind.Angle:
            case FamilyValueKind.Area:
            case FamilyValueKind.Volume:
                return "Dimensions";
            case FamilyValueKind.Material:
                return "Materials";
            case FamilyValueKind.YesNo:
                return "Visibility";
            default:
                return "Data";
        }
    }

    public static FamilyParameter Add(FamilyManager manager, string name, FamilyValueKind kind, string group, bool isInstance)
    {
#if REVIT2022_OR_GREATER
        return manager.AddParameter(name, ResolveGroup(group), ToSpec(kind), isInstance);
#else
        return manager.AddParameter(name, ResolveGroup(group), ToParameterType(kind), isInstance);
#endif
    }

    /// <summary>Добавляет общий параметр из файла общих параметров.</summary>
    public static FamilyParameter AddShared(FamilyManager manager, ExternalDefinition definition, string group, bool isInstance)
    {
        return manager.AddParameter(definition, ResolveGroup(group), isInstance);
    }

    public static FamilyValueKind Classify(FamilyParameter parameter)
    {
#if REVIT2022_OR_GREATER
        var dataType = parameter.Definition.GetDataType();
        if (dataType.Equals(SpecTypeId.Length)) return FamilyValueKind.Length;
        if (dataType.Equals(SpecTypeId.Angle)) return FamilyValueKind.Angle;
        if (dataType.Equals(SpecTypeId.Area)) return FamilyValueKind.Area;
        if (dataType.Equals(SpecTypeId.Volume)) return FamilyValueKind.Volume;
        if (dataType.Equals(SpecTypeId.Number)) return FamilyValueKind.Number;
        if (dataType.Equals(SpecTypeId.Int.Integer)) return FamilyValueKind.Integer;
        if (dataType.Equals(SpecTypeId.Boolean.YesNo)) return FamilyValueKind.YesNo;
        if (dataType.Equals(SpecTypeId.String.Text)) return FamilyValueKind.Text;
        if (dataType.Equals(SpecTypeId.Reference.Material)) return FamilyValueKind.Material;
#else
        switch (parameter.Definition.ParameterType)
        {
            case ParameterType.Length: return FamilyValueKind.Length;
            case ParameterType.Angle: return FamilyValueKind.Angle;
            case ParameterType.Area: return FamilyValueKind.Area;
            case ParameterType.Volume: return FamilyValueKind.Volume;
            case ParameterType.Number: return FamilyValueKind.Number;
            case ParameterType.Integer: return FamilyValueKind.Integer;
            case ParameterType.YesNo: return FamilyValueKind.YesNo;
            case ParameterType.Text: return FamilyValueKind.Text;
            case ParameterType.Material: return FamilyValueKind.Material;
        }
#endif
        return FamilyValueKind.Other;
    }

    public static string DescribeGroup(FamilyParameter parameter)
    {
#if REVIT2022_OR_GREATER
        var groupId = parameter.Definition.GetGroupTypeId();
        try
        {
            return LabelUtils.GetLabelForGroup(groupId);
        }
        catch (Exception)
        {
            return groupId.TypeId;
        }
#else
        return LabelUtils.GetLabelFor(parameter.Definition.ParameterGroup);
#endif
    }

    public static double ToInternal(FamilyValueKind kind, double value)
    {
        switch (kind)
        {
            case FamilyValueKind.Length: return value / MillimetersPerFoot;
            case FamilyValueKind.Area: return value / SquareMetersPerSquareFoot;
            case FamilyValueKind.Volume: return value / CubicMetersPerCubicFoot;
            case FamilyValueKind.Angle: return value * Math.PI / 180.0;
            default: return value;
        }
    }

    public static double FromInternal(FamilyValueKind kind, double value)
    {
        switch (kind)
        {
            case FamilyValueKind.Length: return value * MillimetersPerFoot;
            case FamilyValueKind.Area: return value * SquareMetersPerSquareFoot;
            case FamilyValueKind.Volume: return value * CubicMetersPerCubicFoot;
            case FamilyValueKind.Angle: return value * 180.0 / Math.PI;
            default: return value;
        }
    }

    public static FamilyParameter Find(FamilyManager manager, string name)
    {
        return manager.Parameters
                   .Cast<FamilyParameter>()
                   .FirstOrDefault(p => string.Equals(p.Definition.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"Параметр семейства '{name}' не найден");
    }

    public static FamilyType FindType(FamilyManager manager, string name)
    {
        return manager.Types
                   .Cast<FamilyType>()
                   .FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
               ?? throw new ArgumentException($"Типоразмер '{name}' не найден в семействе");
    }

    public static List<FamilyType> AllTypes(FamilyManager manager)
    {
        return manager.Types.Cast<FamilyType>().ToList();
    }

    public static object ReadValue(Document doc, FamilyType type, FamilyParameter parameter, FamilyValueKind kind)
    {
        if (!type.HasValue(parameter)) return null;
        switch (parameter.StorageType)
        {
            case StorageType.Double:
                var number = type.AsDouble(parameter);
                return number.HasValue ? Math.Round(FromInternal(kind, number.Value), 6) : (object)null;
            case StorageType.Integer:
                var integer = type.AsInteger(parameter);
                if (!integer.HasValue) return null;
                return kind == FamilyValueKind.YesNo ? integer.Value == 1 : (object)integer.Value;
            case StorageType.String:
                return type.AsString(parameter);
            case StorageType.ElementId:
                var id = type.AsElementId(parameter);
                if (id == null || id == ElementId.InvalidElementId) return null;
                return doc.GetElement(id)?.Name ?? (object)id.GetValue();
            default:
                return null;
        }
    }

    public static void SetCurrentTypeValue(Document doc, FamilyManager manager, FamilyParameter parameter, JToken value)
    {
        var name = parameter.Definition.Name;
        if (parameter.IsDeterminedByFormula)
        {
            throw new InvalidOperationException($"Значение '{name}' задаётся формулой '{parameter.Formula}'. Сначала очистите формулу.");
        }

        if (parameter.IsReporting)
        {
            throw new InvalidOperationException($"'{name}' — параметр отчёта, его значение задаётся геометрией");
        }

        var kind = Classify(parameter);
        switch (parameter.StorageType)
        {
            case StorageType.Double:
                manager.Set(parameter, ToInternal(kind, value.Value<double>()));
                break;
            case StorageType.Integer:
                manager.Set(parameter, kind == FamilyValueKind.YesNo ? (ToBool(value) ? 1 : 0) : value.Value<int>());
                break;
            case StorageType.String:
                manager.Set(parameter, value.Type == JTokenType.Null ? string.Empty : value.Value<string>());
                break;
            case StorageType.ElementId:
                manager.Set(parameter, ResolveElementId(doc, value, kind));
                break;
            default:
                throw new InvalidOperationException($"Тип хранения параметра '{name}' не поддерживается");
        }
    }

    public static void SetForTypes(Document doc, FamilyManager manager, FamilyParameter parameter, JToken value, IEnumerable<FamilyType> types)
    {
        var original = manager.CurrentType;
        try
        {
            foreach (var type in types)
            {
                manager.CurrentType = type;
                SetCurrentTypeValue(doc, manager, parameter, value);
            }
        }
        finally
        {
            if (original != null) manager.CurrentType = original;
        }
    }

    public static object Describe(Document doc, FamilyParameter parameter, IList<FamilyType> types)
    {
        var kind = Classify(parameter);
        var values = new Dictionary<string, object>();
        foreach (var type in types)
        {
            var key = string.IsNullOrEmpty(type.Name) ? "<без имени>" : type.Name;
            values[key] = new
            {
                value = ReadValue(doc, type, parameter, kind),
                display = type.HasValue(parameter) ? type.AsValueString(parameter) : null
            };
        }

        return new
        {
            name = parameter.Definition.Name,
            id = parameter.Id.GetValue(),
            kind = kind.ToString(),
            storageType = parameter.StorageType.ToString(),
            group = DescribeGroup(parameter),
            isInstance = parameter.IsInstance,
            isShared = parameter.IsShared,
            isReporting = parameter.IsReporting,
            formula = parameter.IsDeterminedByFormula ? parameter.Formula : null,
            values
        };
    }

    private static bool ToBool(JToken value)
    {
        switch (value.Type)
        {
            case JTokenType.Boolean:
                return value.Value<bool>();
            case JTokenType.Integer:
            case JTokenType.Float:
                return Math.Abs(value.Value<double>()) > 0.5;
            default:
                var text = value.Value<string>()?.Trim().ToLowerInvariant();
                if (text == "true" || text == "1" || text == "да" || text == "yes") return true;
                if (text == "false" || text == "0" || text == "нет" || text == "no") return false;
                throw new ArgumentException($"Не удалось интерпретировать '{value}' как Да/Нет");
        }
    }

    private static ElementId ResolveElementId(Document doc, JToken value, FamilyValueKind kind)
    {
        if (value.Type == JTokenType.Null) return ElementId.InvalidElementId;
        if (value.Type == JTokenType.Integer) return FamilyEditorIds.FromNumber(value.Value<long>());
        if (kind == FamilyValueKind.Material) return FamilyEditorIds.FindMaterial(doc, value.Value<string>()).Id;
        throw new ArgumentException("Для параметров-ссылок, кроме материала, передайте числовой ElementId");
    }

#if REVIT2022_OR_GREATER
    private static ForgeTypeId ToSpec(FamilyValueKind kind)
    {
        switch (kind)
        {
            case FamilyValueKind.Length: return SpecTypeId.Length;
            case FamilyValueKind.Angle: return SpecTypeId.Angle;
            case FamilyValueKind.Area: return SpecTypeId.Area;
            case FamilyValueKind.Volume: return SpecTypeId.Volume;
            case FamilyValueKind.Number: return SpecTypeId.Number;
            case FamilyValueKind.Integer: return SpecTypeId.Int.Integer;
            case FamilyValueKind.YesNo: return SpecTypeId.Boolean.YesNo;
            case FamilyValueKind.Text: return SpecTypeId.String.Text;
            case FamilyValueKind.Material: return SpecTypeId.Reference.Material;
            default: throw new ArgumentException($"Тип '{kind}' нельзя создать");
        }
    }

    private static ForgeTypeId ResolveGroup(string group)
    {
        switch (NormalizeGroup(group))
        {
            case "dimensions": return GroupTypeId.Geometry;
            case "constraints": return GroupTypeId.Constraints;
            case "data": return GroupTypeId.Data;
            case "text": return GroupTypeId.Text;
            case "identitydata": return GroupTypeId.IdentityData;
            case "materials": return GroupTypeId.Materials;
            case "graphics": return GroupTypeId.Graphics;
            case "construction": return GroupTypeId.Construction;
            case "visibility": return GroupTypeId.Visibility;
            case "other": return new ForgeTypeId(string.Empty);
            default: throw UnknownGroup(group);
        }
    }
#else
    private static ParameterType ToParameterType(FamilyValueKind kind)
    {
        switch (kind)
        {
            case FamilyValueKind.Length: return ParameterType.Length;
            case FamilyValueKind.Angle: return ParameterType.Angle;
            case FamilyValueKind.Area: return ParameterType.Area;
            case FamilyValueKind.Volume: return ParameterType.Volume;
            case FamilyValueKind.Number: return ParameterType.Number;
            case FamilyValueKind.Integer: return ParameterType.Integer;
            case FamilyValueKind.YesNo: return ParameterType.YesNo;
            case FamilyValueKind.Text: return ParameterType.Text;
            case FamilyValueKind.Material: return ParameterType.Material;
            default: throw new ArgumentException($"Тип '{kind}' нельзя создать");
        }
    }

    private static BuiltInParameterGroup ResolveGroup(string group)
    {
        switch (NormalizeGroup(group))
        {
            case "dimensions": return BuiltInParameterGroup.PG_GEOMETRY;
            case "constraints": return BuiltInParameterGroup.PG_CONSTRAINTS;
            case "data": return BuiltInParameterGroup.PG_DATA;
            case "text": return BuiltInParameterGroup.PG_TEXT;
            case "identitydata": return BuiltInParameterGroup.PG_IDENTITY_DATA;
            case "materials": return BuiltInParameterGroup.PG_MATERIALS;
            case "graphics": return BuiltInParameterGroup.PG_GRAPHICS;
            case "construction": return BuiltInParameterGroup.PG_CONSTRUCTION;
            case "visibility": return BuiltInParameterGroup.PG_VISIBILITY;
            case "other": return BuiltInParameterGroup.INVALID;
            default: throw UnknownGroup(group);
        }
    }
#endif

    private static string NormalizeGroup(string group)
    {
        return (group ?? string.Empty).Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    private static ArgumentException UnknownGroup(string group)
    {
        return new ArgumentException($"Неизвестная группа '{group}'. Допустимо: {string.Join(", ", GroupNames)}");
    }
}
