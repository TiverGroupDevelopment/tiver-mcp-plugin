using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

/// <summary>Свойства семейства (категория и параметры из окна «Категория и параметры семейства»).</summary>
public static class FamilyProperties
{
    private sealed class Property
    {
        public Property(string key, BuiltInParameter parameter, bool isText = false)
        {
            Key = key;
            Parameter = parameter;
            IsText = isText;
        }

        public string Key { get; }
        public BuiltInParameter Parameter { get; }
        public bool IsText { get; }
    }

    private static readonly Property[] Writable =
    {
        new Property("workPlaneBased", BuiltInParameter.FAMILY_WORK_PLANE_BASED),
        new Property("alwaysVertical", BuiltInParameter.FAMILY_ALWAYS_VERTICAL),
        new Property("shared", BuiltInParameter.FAMILY_SHARED),
        new Property("roomCalculationPoint", BuiltInParameter.ROOM_CALCULATION_POINT),
        new Property("cutWithVoidsWhenLoaded", BuiltInParameter.FAMILY_ALLOW_CUT_WITH_VOIDS),
#if !REVIT2026_OR_GREATER
        // В API Revit 2026 параметры OmniClass удалены.
        new Property("omniClassNumber", BuiltInParameter.OMNICLASS_CODE, true),
        new Property("omniClassTitle", BuiltInParameter.OMNICLASS_DESCRIPTION, true)
#endif
    };

    public static IEnumerable<string> WritableKeys => Writable.Select(p => p.Key);

    public static object Describe(Document doc)
    {
        var family = doc.OwnerFamily ?? throw new InvalidOperationException("У документа нет семейства-владельца");
        var category = family.FamilyCategory;
        var values = new Dictionary<string, object>();
        foreach (var property in Writable)
        {
            var parameter = family.get_Parameter(property.Parameter);
            if (parameter == null) continue;
            values[property.Key] = new
            {
                value = property.IsText ? (object)parameter.AsString() : parameter.AsInteger() == 1,
                readOnly = parameter.IsReadOnly
            };
        }

        var hosting = family.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR);
        var partType = family.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);

        return new
        {
            name = family.Name,
            category = category?.Name,
            builtInCategory = category == null ? null : BuiltInCategoryName(category),
            placementType = family.FamilyPlacementType.ToString(),
            hosting = hosting?.AsValueString(),
            partType = partType?.AsValueString(),
            isEditable = family.IsEditable,
            properties = values,
            subcategories = Subcategories(category).Select(s => new { s.name, s.id }).ToList()
        };
    }

    public static List<string> Apply(Document doc, JObject args)
    {
        var family = doc.OwnerFamily ?? throw new InvalidOperationException("У документа нет семейства-владельца");
        var changed = new List<string>();

        var categoryName = JsonArgs.OptionalString(args, "category");
        if (categoryName != null)
        {
            family.FamilyCategory = ResolveCategory(doc, categoryName);
            changed.Add("category");
        }

        foreach (var property in Writable)
        {
            if (!JsonArgs.Has(args, property.Key)) continue;
            var parameter = family.get_Parameter(property.Parameter)
                            ?? throw new InvalidOperationException($"Свойство '{property.Key}' недоступно для этой категории семейства");
            if (parameter.IsReadOnly)
            {
                throw new InvalidOperationException($"Свойство '{property.Key}' нельзя изменить для этой категории или шаблона семейства");
            }

            if (property.IsText)
            {
                parameter.Set(args[property.Key].Value<string>() ?? string.Empty);
            }
            else
            {
                parameter.Set(args[property.Key].Value<bool>() ? 1 : 0);
            }

            changed.Add(property.Key);
        }

        return changed;
    }

    /// <summary>Категория по локализованному имени или по BuiltInCategory (OST_GenericModel).</summary>
    public static Category ResolveCategory(Document doc, string name)
    {
        var categories = doc.Settings.Categories;
        if (Enum.TryParse(name, true, out BuiltInCategory builtIn) && !int.TryParse(name, out _))
        {
            try
            {
                var byEnum = categories.get_Item(builtIn);
                if (byEnum != null) return byEnum;
            }
            catch (Exception)
            {
            }
        }

        foreach (Category category in categories)
        {
            if (string.Equals(category.Name, name, StringComparison.OrdinalIgnoreCase)) return category;
        }

        throw new ArgumentException($"Категория '{name}' не найдена. Передайте имя как в Revit или BuiltInCategory, например OST_GenericModel.");
    }

    /// <summary>Подкатегория семейства по имени; null → ошибка со списком доступных.</summary>
    public static Category FindSubcategory(Document doc, string name)
    {
        var parent = doc.OwnerFamily?.FamilyCategory
                     ?? throw new InvalidOperationException("У семейства не задана категория");
        foreach (Category subcategory in parent.SubCategories)
        {
            if (string.Equals(subcategory.Name, name, StringComparison.OrdinalIgnoreCase)) return subcategory;
        }

        throw new ArgumentException(
            $"Подкатегория '{name}' не найдена в категории '{parent.Name}'. Доступные: {string.Join(", ", Subcategories(parent).Select(s => s.name))}. Создать: create_family_subcategories.");
    }

    public static List<(string name, long id)> Subcategories(Category parent)
    {
        var result = new List<(string name, long id)>();
        if (parent == null) return result;
        foreach (Category subcategory in parent.SubCategories)
        {
            result.Add((subcategory.Name, subcategory.Id.GetValue()));
        }

        return result.OrderBy(s => s.name).ToList();
    }

    private static string BuiltInCategoryName(Category category)
    {
        var value = category.Id.GetValue();
        return value < 0 && Enum.IsDefined(typeof(BuiltInCategory), (int)value) ? ((BuiltInCategory)(int)value).ToString() : null;
    }
}
