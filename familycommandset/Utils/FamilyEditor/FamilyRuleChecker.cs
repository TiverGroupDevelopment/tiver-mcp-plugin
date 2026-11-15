using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

/// <summary>Результат одной проверки правила.</summary>
public sealed class RuleResult
{
    public string Rule { get; set; }
    public string Check { get; set; }
    /// <summary>pass | warn | fail | skip</summary>
    public string Status { get; set; }
    public string Summary { get; set; }
    public List<string> Details { get; } = new List<string>();

    public object Describe() => new { rule = Rule, check = Check, status = Status, summary = Summary, details = Details };

    public RuleResult WithDetails(IEnumerable<string> details)
    {
        Details.AddRange(details);
        return this;
    }
}

/// <summary>
/// Универсальные проверки семейства. Какие проверки запускать и с какими порогами, задаёт профиль правил
/// (rules/&lt;проект&gt;/profile.json → checks): в коде нет правил конкретного проекта.
/// Проверки только читают документ.
/// </summary>
public static class FamilyRuleChecker
{
    public static List<RuleResult> Run(Document doc, JObject checks)
    {
        var results = new List<RuleResult>();
        if (checks == null) return results;

        void Add(string key, Func<JObject, RuleResult> check)
        {
            if (!(checks[key] is JObject spec)) return;
            RuleResult result;
            try
            {
                result = check(spec);
            }
            catch (Exception ex)
            {
                result = new RuleResult { Status = "skip", Summary = "Проверка не выполнена: " + ex.Message };
            }

            result.Rule = spec["rule"]?.Value<string>() ?? key;
            result.Check = key;
            results.Add(result);
        }

        var forms = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements().OfType<GenericForm>().ToList();
        var solidBox = SolidBox(forms);

        Add("category", spec => Category(doc, spec));
        Add("requiredParameters", spec => RequiredParameters(doc, spec));
        Add("materialParameter", spec => MaterialParameter(doc, forms, spec));
        Add("lookupTable", spec => LookupTable(doc, spec));
        Add("nesting", spec => Nesting(doc, spec));
        Add("nestedShared", spec => NestedShared(doc));
        Add("connectorSystem", spec => ConnectorSystem(doc, spec));
        Add("connectorsLinked", spec => ConnectorsLinked(doc));
        Add("connectorOrientation", spec => ConnectorOrientation(doc, spec));
        Add("singleGeometry", spec => SingleGeometry(doc, forms, spec));
        Add("labelsOnPlanes", spec => LabelsOnPlanes(doc, spec));
        Add("origin", spec => Origin(solidBox, spec));
        Add("referencePlanes", spec => ReferencePlanes(doc, solidBox, spec));
        Add("parameterGroups", spec => ParameterGroups(doc, spec));
        Add("extraParameters", spec => ExtraParameters(doc, spec));
        Add("lookupDefaults", spec => LookupDefaults(doc, spec));
        Add("lookupDecimals", spec => LookupDecimals(doc, spec));
        Add("connectorDepth", spec => ConnectorDepth(doc, solidBox, spec));
        return results;
    }

    private static RuleResult Pass(string summary) => new RuleResult { Status = "pass", Summary = summary };
    private static RuleResult Of(string status, string summary, IEnumerable<string> details = null)
    {
        var r = new RuleResult { Status = status, Summary = summary };
        if (details != null) r.Details.AddRange(details);
        return r;
    }

    private static List<string> Strings(JObject spec, string key) =>
        (spec[key] as JArray)?.Select(t => t.Value<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();

    private static RuleResult Category(Document doc, JObject spec)
    {
        var category = doc.OwnerFamily?.FamilyCategory;
        var builtIn = FamilyProperties.BuiltInCategoryName(category);
        var allowed = Strings(spec, "allowed");
        var partType = doc.OwnerFamily?.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
        var partName = partType == null ? null : Enum.GetName(typeof(PartType), partType.AsInteger());
        var info = $"категория «{category?.Name}» ({builtIn}), тип детали {partName ?? "—"}";
        if (allowed.Count > 0 && !allowed.Contains(builtIn, StringComparer.OrdinalIgnoreCase))
        {
            return Of("fail", $"Неожиданная категория: {info}. Допустимо: {string.Join(", ", allowed)}");
        }

        return Pass(info);
    }

    private static RuleResult RequiredParameters(Document doc, JObject spec)
    {
        var manager = doc.FamilyManager;
        var byName = manager.Parameters.Cast<FamilyParameter>().GroupBy(p => p.Definition.Name).ToDictionary(g => g.Key, g => g.First());
        var problems = new List<string>();
        var notes = new List<string>();
        var required = (spec["parameters"] as JArray)?.OfType<JObject>().ToList() ?? new List<JObject>();
        foreach (var item in required)
        {
            var name = item["name"]?.Value<string>();
            if (name == null) continue;
            if (!byName.TryGetValue(name, out var parameter))
            {
                problems.Add($"{name}: нет в семействе");
                continue;
            }

            var guid = item["guid"]?.Value<string>();
            if (guid != null)
            {
                if (!parameter.IsShared) problems.Add($"{name}: параметр семейства, а нужен общий из ФОП");
                else if (!string.Equals(parameter.GUID.ToString(), guid, StringComparison.OrdinalIgnoreCase))
                    problems.Add($"{name}: GUID {parameter.GUID} не совпадает с ФОП ({guid})");
            }

            var instance = item["instance"]?.Value<bool?>();
            if (instance.HasValue && instance.Value != parameter.IsInstance)
            {
                notes.Add($"{name}: {(parameter.IsInstance ? "экземпляр" : "тип")}, ожидается {(instance.Value ? "экземпляр" : "тип")}");
            }
        }

        if (problems.Count > 0) return Of("fail", $"Обязательные параметры: проблем {problems.Count} из {required.Count}", problems.Concat(notes));
        if (notes.Count > 0) return Of("warn", "Обязательные параметры есть, но тип/экземпляр отличается от ожидаемого", notes);
        return Pass($"Все {required.Count} обязательных параметров есть и совпадают с ФОП");
    }

    private static RuleResult MaterialParameter(Document doc, List<GenericForm> forms, JObject spec)
    {
        var name = spec["name"]?.Value<string>() ?? throw new ArgumentException("materialParameter.name не задан");
        var manager = doc.FamilyManager;
        var parameter = manager.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name == name);
        if (parameter == null) return Of("fail", $"Нет параметра {name}");

        var problems = new List<string>();
        var guid = spec["guid"]?.Value<string>();
        if (guid != null && (!parameter.IsShared || !string.Equals(parameter.GUID.ToString(), guid, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add($"{name} не общий параметр из ФОП");
        }

        foreach (var form in forms.Where(f => f.IsSolid))
        {
            var material = form.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
            var associated = material == null ? null : manager.GetAssociatedFamilyParameter(material);
            if (associated == null || associated.Definition.Name != name)
            {
                problems.Add($"{form.GetType().Name} {form.Id.GetValue()}: материал не связан с {name} ({associated?.Definition.Name ?? "не связан"})");
            }
        }

        foreach (var instance in new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
        {
            var nestedParameter = instance.Symbol.LookupParameter(name) ?? instance.LookupParameter(name);
            if (nestedParameter == null) continue;
            var associated = manager.GetAssociatedFamilyParameter(nestedParameter);
            if (associated == null || associated.Definition.Name != name)
            {
                problems.Add($"Вложенное «{instance.Symbol.Family.Name}»: {name} не связан с параметром основного семейства");
            }
        }

        return problems.Count == 0
            ? Pass($"Материал всех твёрдых форм и вложенных семейств управляется {name}")
            : Of("fail", $"Материал не везде управляется {name}", problems);
    }

    private static RuleResult LookupTable(Document doc, JObject spec)
    {
        var manager = FamilySizeTableManager.GetFamilySizeTableManager(doc, doc.OwnerFamily.Id);
        var tables = manager?.GetAllSizeTableNames()?.ToList() ?? new List<string>();
        var types = doc.FamilyManager.Types.Size;
        var maxTypes = spec["maxTypes"]?.Value<int?>() ?? 2;
        var formulas = doc.FamilyManager.Parameters.Cast<FamilyParameter>()
            .Count(p => (p.Formula ?? string.Empty).IndexOf("size_lookup", StringComparison.OrdinalIgnoreCase) >= 0);
        var details = new List<string> { $"таблиц: {tables.Count} ({string.Join(", ", tables)})", $"типоразмеров: {types}", $"параметров с size_lookup: {formulas}" };

        if (tables.Count == 0 || formulas == 0)
        {
            return Of(types > maxTypes ? "fail" : "warn", "Размеры не из таблицы выбора (нет таблицы или формул size_lookup)", details);
        }

        if (types > maxTypes) return Of("warn", $"Таблица есть, но типоразмеров {types} (ожидается не больше {maxTypes})", details);
        return Pass($"Размеры из таблицы выбора, типоразмеров {types}");
    }

    private static RuleResult Nesting(Document doc, JObject spec)
    {
        var maxDepth = spec["maxDepth"]?.Value<int?>() ?? 1;
        var maxCount = spec["maxCount"]?.Value<int?>() ?? 3;
        var instances = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToList();
        var details = new List<string>();
        var depth = instances.Count == 0 ? 0 : 1 + MaxNestedDepth(doc, instances, maxDepth + 1, details, 1);
        var families = instances.Select(i => i.Symbol.Family.Name).Distinct().ToList();
        details.Insert(0, $"вложенных экземпляров: {instances.Count}, семейств: {families.Count} ({string.Join(", ", families)}), глубина: {depth}");

        if (depth > maxDepth) return Of("fail", $"Вложенность {depth} уровня (допустимо {maxDepth})", details);
        if (instances.Count > maxCount) return Of("warn", $"Много вложенных экземпляров: {instances.Count} (ориентир — не больше {maxCount})", details);
        return Pass(instances.Count == 0 ? "Вложенных семейств нет" : $"Вложенность {depth}, экземпляров {instances.Count}");
    }

    private static int MaxNestedDepth(Document doc, List<FamilyInstance> instances, int limit, List<string> details, int level)
    {
        if (level >= limit) return 0;
        var max = 0;
        foreach (var family in instances.Select(i => i.Symbol.Family).GroupBy(f => f.Id.GetValue()).Select(g => g.First()))
        {
            if (!family.IsEditable) continue;
            Document nested = null;
            try
            {
                nested = doc.EditFamily(family);
                var inner = new FilteredElementCollector(nested).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToList();
                if (inner.Count == 0) continue;
                details.Add($"«{family.Name}» содержит вложенные: {string.Join(", ", inner.Select(i => i.Symbol.Family.Name).Distinct())}");
                max = Math.Max(max, 1 + MaxNestedDepth(nested, inner, limit, details, level + 1));
            }
            catch (Exception ex)
            {
                details.Add($"«{family.Name}»: не удалось открыть ({ex.Message})");
            }
            finally
            {
                nested?.Close(false);
            }
        }

        return max;
    }

    private static RuleResult NestedShared(Document doc)
    {
        var shared = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>()
            .Where(f => f.Id != doc.OwnerFamily.Id && f.get_Parameter(BuiltInParameter.FAMILY_SHARED)?.AsInteger() == 1)
            .Select(f => f.Name)
            .ToList();
        return shared.Count == 0
            ? Pass("Ни одно вложенное семейство не отмечено «Общий»")
            : Of("fail", "Вложенные семейства с галочкой «Общий»: их экземпляры попадут в спецификации проекта отдельно", shared);
    }

    private static List<ConnectorElement> Connectors(Document doc) =>
        new FilteredElementCollector(doc).OfClass(typeof(ConnectorElement)).Cast<ConnectorElement>().ToList();

    private static RuleResult ConnectorSystem(Document doc, JObject spec)
    {
        var wanted = spec["pipe"]?.Value<string>() ?? "Fitting";
        var connectors = Connectors(doc);
        if (connectors.Count == 0) return Of(spec["required"]?.Value<bool?>() == false ? "pass" : "fail", "В семействе нет коннекторов");
        var wrong = connectors
            .Where(c => c.Domain == Domain.DomainPiping && !string.Equals(c.SystemClassification.ToString(), wanted, StringComparison.OrdinalIgnoreCase))
            .Select(c => $"коннектор {c.Id.GetValue()}: {c.SystemClassification}")
            .ToList();
        return wrong.Count == 0
            ? Pass($"Трубные коннекторы с классификацией {wanted} ({connectors.Count} шт.)")
            : Of("fail", $"Классификация системы трубных коннекторов должна быть {wanted}", wrong);
    }

    private static RuleResult ConnectorsLinked(Document doc)
    {
        var connectors = Connectors(doc);
        if (connectors.Count < 2) return Of("pass", connectors.Count == 0 ? "Коннекторов нет" : "Один коннектор — связывать не с чем");
        var unlinked = new List<string>();
        foreach (var connector in connectors)
        {
            Element linked = null;
            var method = connector.GetType().GetMethod("GetLinkedConnectorElement");
            if (method == null) return Of("skip", "В этой версии Revit API нет связи коннекторов");
            linked = method.Invoke(connector, null) as Element;
            if (linked == null) unlinked.Add($"коннектор {connector.Id.GetValue()} ({connector.Domain}) не связан");
        }

        return unlinked.Count == 0
            ? Pass($"Все {connectors.Count} коннектора связаны попарно")
            : Of("fail", "Есть несвязанные коннекторы (поток не проходит через деталь)", unlinked);
    }

    private static RuleResult ConnectorOrientation(Document doc, JObject spec)
    {
        var up = spec["up"] != null ? JsonArgs.Vector(spec["up"], "up").Normalize() : XYZ.BasisZ;
        var connectors = Connectors(doc);
        if (connectors.Count == 0) return Of("pass", "Коннекторов нет");
        var wrong = new List<string>();
        foreach (var connector in connectors)
        {
            var system = connector.CoordinateSystem;
            var y = system.BasisY.Normalize();
            if (y.DotProduct(up) < 0.99)
            {
                wrong.Add($"коннектор {connector.Id.GetValue()}: ось Y = [{Math.Round(y.X, 3)}, {Math.Round(y.Y, 3)}, {Math.Round(y.Z, 3)}], направление Z = [{Math.Round(system.BasisZ.X, 3)}, {Math.Round(system.BasisZ.Y, 3)}, {Math.Round(system.BasisZ.Z, 3)}]");
            }
        }

        return wrong.Count == 0
            ? Pass("Ось Y всех коннекторов направлена вверх")
            : Of("fail", "Ось Y коннектора не направлена вверх: при вставке деталь повернётся вокруг трубы", wrong);
    }

    private static RuleResult SingleGeometry(Document doc, List<GenericForm> forms, JObject spec)
    {
        var maxForms = spec["maxForms"]?.Value<int?>() ?? 6;
        var details = new List<string>
        {
            $"форм: {forms.Count} ({string.Join(", ", forms.GroupBy(f => f.GetType().Name).Select(g => g.Key + " " + g.Count()))})"
        };
        var forbidden = Strings(spec, "forbid");
        var all = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements();
        var bad = all.Where(e => forbidden.Contains(e.GetType().Name)).GroupBy(e => e.GetType().Name).Select(g => $"{g.Key}: {g.Count()}").ToList();
        if (bad.Count > 0) return Of("fail", "Запрещённые элементы геометрии", bad.Concat(details));
        if (forms.Count > maxForms) return Of("warn", $"Форм {forms.Count} — больше ориентира {maxForms}: проверьте, нельзя ли объединить в одно тело", details);
        return Pass(details[0]);
    }

    private static RuleResult LabelsOnPlanes(Document doc, JObject spec)
    {
        var allowed = Strings(spec, "allowGeometryLabels");
        var onGeometry = new List<string>();
        var onPlanes = 0;
        foreach (var dimension in new FilteredElementCollector(doc).OfClass(typeof(Dimension)).Cast<Dimension>())
        {
            FamilyParameter label = null;
            try
            {
                label = dimension.FamilyLabel;
            }
            catch (Exception)
            {
            }

            if (label == null) continue;
            var kinds = new List<string>();
            foreach (Reference reference in dimension.References)
            {
                var element = doc.GetElement(reference.ElementId);
                kinds.Add(element?.GetType().Name ?? "?");
            }

            if (kinds.All(k => k == nameof(ReferencePlane) || k == nameof(Level)))
            {
                onPlanes++;
                continue;
            }

            var name = label.Definition.Name;
            if (allowed.Any(a => name.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
            onGeometry.Add($"«{name}» привязан к {string.Join(" и ", kinds.Distinct())}");
        }

        return onGeometry.Count == 0
            ? Pass($"Параметры управляют опорными плоскостями (размеров с метками на плоскостях: {onPlanes})")
            : Of("warn", "Параметры на геометрии, а не на опорных плоскостях (кроме разрешённых исключений)", onGeometry.Distinct());
    }

    private static RuleResult Origin(BoundingBoxXYZ box, JObject spec)
    {
        if (box == null) return Of("skip", "Нет твёрдой геометрии");
        var tolerance = (spec["toleranceMm"]?.Value<double?>() ?? 0.5) / 304.8;
        var axes = Strings(spec, "axes");
        if (axes.Count == 0) axes = new List<string> { "X", "Y", "Z" };
        var center = (box.Min + box.Max) / 2;
        var offsets = new List<string>();
        foreach (var axis in axes)
        {
            var value = axis.ToUpperInvariant() == "X" ? center.X : axis.ToUpperInvariant() == "Y" ? center.Y : center.Z;
            if (Math.Abs(value) > tolerance) offsets.Add($"{axis}: центр габаритов смещён на {Math.Round(value * 304.8, 2)} мм");
        }

        var summary = $"центр габаритов [{Math.Round(center.X * 304.8, 2)}, {Math.Round(center.Y * 304.8, 2)}, {Math.Round(center.Z * 304.8, 2)}] мм";
        return offsets.Count == 0
            ? Pass($"Семейство в центре пересечения опорных плоскостей ({summary})")
            : Of("fail", "Семейство не в центре пересечения опорных плоскостей: " + summary, offsets);
    }

    private static RuleResult ReferencePlanes(Document doc, BoundingBoxXYZ box, JObject spec)
    {
        if (box == null) return Of("skip", "Нет твёрдой геометрии");
        var factor = spec["maxLengthFactor"]?.Value<double?>() ?? 3;
        var ignoreTemplate = spec["ignoreTemplatePlanes"]?.Value<bool?>() ?? true;
        var size = box.Max - box.Min;
        var maxSize = Math.Max(size.X, Math.Max(size.Y, size.Z));
        var tooLong = new List<string>();
        var count = 0;
        var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>()
            .Where(v => !v.IsTemplate && (v is ViewPlan || v.ViewType == ViewType.Elevation || v.ViewType == ViewType.Section))
            .ToList();
        foreach (var plane in new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>())
        {
            var definesOrigin = plane.get_Parameter(BuiltInParameter.DATUM_PLANE_DEFINES_ORIGIN)?.AsInteger() == 1;
            if (ignoreTemplate && definesOrigin) continue;
            count++;
            // Длина на виде создания и модельные границы в остальных видах (поперёк Revit по умолчанию тянет плоскость на 9 м).
            var length = plane.BubbleEnd.DistanceTo(plane.FreeEnd);
            var where = "на виде создания";
            foreach (var view in views)
            {
                try
                {
                    if (!plane.CanBeVisibleInView(view)) continue;
                    foreach (var curve in plane.GetCurvesInView(DatumExtentType.Model, view))
                    {
                        if (curve.Length > length)
                        {
                            length = curve.Length;
                            where = $"в виде «{view.Name}»";
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            if (length > maxSize * factor)
            {
                tooLong.Add($"«{(string.IsNullOrEmpty(plane.Name) ? "без имени" : plane.Name)}» id {plane.Id.GetValue()}: {Math.Round(length * 304.8, 1)} мм {where} при габарите {Math.Round(maxSize * 304.8, 1)} мм");
            }
        }

        return tooLong.Count == 0
            ? Pass($"Созданные опорные плоскости ({count}) не длиннее {factor}× габарита")
            : Of(spec["severity"]?.Value<string>() ?? "warn", $"Опорные плоскости намного длиннее геометрии (> {factor}× габарита)", tooLong);
    }

    /// <summary>Группа параметра на палитре свойств: {"parameters": {"ADSK_Материал": "Materials"}}.</summary>
    private static RuleResult ParameterGroups(Document doc, JObject spec)
    {
        var wanted = spec["parameters"] as JObject ?? throw new ArgumentException("parameterGroups.parameters не задан");
        var manager = doc.FamilyManager;
        var problems = new List<string>();
        foreach (var item in wanted.Properties())
        {
            var parameter = manager.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name == item.Name);
            if (parameter == null) continue;
            var group = item.Value.Value<string>();
            if (!FamilyParameterSpecs.IsInGroup(parameter, group))
            {
                problems.Add($"{item.Name}: в группе «{FamilyParameterSpecs.DescribeGroup(parameter)}», ожидается {group}");
            }
        }

        return problems.Count == 0
            ? Pass($"Группы параметров как в профиле ({wanted.Count} шт.)")
            : Of(spec["severity"]?.Value<string>() ?? "fail", "Параметры не в своей группе", problems);
    }

    /// <summary>Лишние параметры: запрещённые имена и текстовые параметры семейства (не общие) вне списка разрешённых.</summary>
    private static RuleResult ExtraParameters(Document doc, JObject spec)
    {
        var forbid = Strings(spec, "forbid");
        var allowText = Strings(spec, "allowText");
        var problems = new List<string>();
        foreach (FamilyParameter parameter in doc.FamilyManager.Parameters)
        {
            var name = parameter.Definition.Name;
            if (forbid.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add($"{name}: параметр не нужен (запрещён профилем)");
                continue;
            }

            if (parameter.IsShared || parameter.Id.GetValue() < 0 || parameter.StorageType != StorageType.String) continue;
            if (allowText.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            problems.Add($"{name}: текстовый параметр семейства вне списка разрешённых ({string.Join(", ", allowText)})");
        }

        return problems.Count == 0
            ? Pass("Лишних параметров нет")
            : Of(spec["severity"]?.Value<string>() ?? "fail", "В семействе лишние параметры", problems);
    }

    /// <summary>
    /// Значение по умолчанию size_lookup у текстов: {"parameters": ["ADSK_Наименование", "ADSK_Марка"], "text": "Нет в каталоге"}.
    /// </summary>
    private static RuleResult LookupDefaults(Document doc, JObject spec)
    {
        var text = spec["text"]?.Value<string>() ?? throw new ArgumentException("lookupDefaults.text не задан");
        var quoted = "\"" + text + "\"";
        var problems = new List<string>();
        var names = Strings(spec, "parameters");
        foreach (var name in names)
        {
            var parameter = doc.FamilyManager.Parameters.Cast<FamilyParameter>().FirstOrDefault(p => p.Definition.Name == name);
            if (parameter == null)
            {
                problems.Add($"{name}: нет в семействе");
                continue;
            }

            var formula = parameter.Formula ?? string.Empty;
            if (formula.IndexOf("size_lookup", StringComparison.OrdinalIgnoreCase) < 0) problems.Add($"{name}: не из таблицы выбора (формула «{formula}»)");
            else if (formula.IndexOf(quoted, StringComparison.Ordinal) < 0) problems.Add($"{name}: значение по умолчанию не {quoted} (формула «{formula}»)");
        }

        return problems.Count == 0
            ? Pass($"Для строк, которых нет в таблице, {string.Join(" и ", names)} пишут {quoted}")
            : Of(spec["severity"]?.Value<string>() ?? "fail", $"Нет значения по умолчанию {quoted}", problems);
    }

    /// <summary>Число знаков после запятой в числах таблиц выбора: {"maxDecimals": 2}.</summary>
    private static RuleResult LookupDecimals(Document doc, JObject spec)
    {
        var maxDecimals = spec["maxDecimals"]?.Value<int?>() ?? 1;
        var manager = FamilySizeTableManager.GetFamilySizeTableManager(doc, doc.OwnerFamily.Id);
        if (manager == null) return Of("skip", "Таблиц выбора нет");
        var problems = new List<string>();
        var cells = 0;
        foreach (var name in manager.GetAllSizeTableNames())
        {
            var table = manager.GetSizeTable(name);
            for (var column = 1; column < table.NumberOfColumns; column++)
            {
                var header = table.GetColumnHeader(column).Name;
                for (var row = 0; row < table.NumberOfRows; row++)
                {
                    var value = table.AsValueString(row, column) ?? string.Empty;
                    if (!double.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) continue;
                    cells++;
                    // Revit 2019 пишет числа таблицы с 6 знаками («20.000000»): значащие — без хвостовых нулей
                    var separator = value.IndexOfAny(new[] { '.', ',' });
                    var decimals = separator < 0 ? 0 : value.Substring(separator + 1).TrimEnd('0').Length;
                    if (decimals > maxDecimals) problems.Add($"{name}: {header}, строка {row + 1} = {value}");
                }
            }
        }

        return problems.Count == 0
            ? Pass($"Числа таблиц выбора не длиннее {maxDecimals} знаков после запятой ({cells} ячеек)")
            : Of(spec["severity"]?.Value<string>() ?? "warn", $"Больше {maxDecimals} знаков после запятой: {problems.Count} ячеек", problems.Take(30));
    }

    /// <summary>
    /// Положение коннекторов относительно торцов детали: коннектор раструба/внутренней резьбы стоит в глубине детали,
    /// а не на торце. {"requireInside": false} — только отчёт; true — warn, если все коннекторы на торцах.
    /// </summary>
    private static RuleResult ConnectorDepth(Document doc, BoundingBoxXYZ box, JObject spec)
    {
        if (box == null) return Of("skip", "Нет твёрдой геометрии");
        var connectors = Connectors(doc);
        if (connectors.Count == 0) return Of("pass", "Коннекторов нет");
        var tolerance = 0.05 / 304.8;
        var details = new List<string>();
        var outside = new List<string>();
        var inside = 0;
        foreach (var connector in connectors)
        {
            var origin = connector.Origin;
            var z = connector.CoordinateSystem.BasisZ.Normalize();
            // Расстояние от коннектора до габарита детали по направлению коннектора (наружу).
            var reach = Reach(box, origin, z);
            var name = $"коннектор {connector.Id.GetValue()} (Ø {Math.Round(connector.get_Parameter(BuiltInParameter.CONNECTOR_DIAMETER)?.AsDouble() * 304.8 ?? 0, 1)})";
            if (reach < -tolerance) outside.Add($"{name}: вне габарита на {Math.Round(-reach * 304.8, 2)} мм");
            else if (reach > tolerance)
            {
                inside++;
                details.Add($"{name}: в глубине на {Math.Round(reach * 304.8, 2)} мм от торца");
            }
            else details.Add($"{name}: на торце");
        }

        if (outside.Count > 0) return Of("fail", "Коннектор вне габарита детали", outside.Concat(details));
        if (inside == 0 && (spec["requireInside"]?.Value<bool?>() ?? false))
        {
            return Of("warn", "Все коннекторы на торцах: у раструбов и внутренней резьбы труба должна входить в деталь", details);
        }

        return Pass($"Коннекторов в глубине детали: {inside} из {connectors.Count}").WithDetails(details);
    }

    private static double Reach(BoundingBoxXYZ box, XYZ origin, XYZ direction)
    {
        double Axis(double o, double d, double min, double max) => d > 1e-9 ? (max - o) / d : d < -1e-9 ? (min - o) / d : double.PositiveInfinity;
        var t = Math.Min(Axis(origin.X, direction.X, box.Min.X, box.Max.X),
            Math.Min(Axis(origin.Y, direction.Y, box.Min.Y, box.Max.Y), Axis(origin.Z, direction.Z, box.Min.Z, box.Max.Z)));
        return double.IsInfinity(t) ? 0 : t;
    }

    private static BoundingBoxXYZ SolidBox(List<GenericForm> forms)
    {
        BoundingBoxXYZ result = null;
        foreach (var form in forms.Where(f => f.IsSolid))
        {
            var b = form.get_BoundingBox(null);
            if (b == null) continue;
            if (result == null)
            {
                result = new BoundingBoxXYZ { Min = b.Min, Max = b.Max };
                continue;
            }

            result.Min = new XYZ(Math.Min(result.Min.X, b.Min.X), Math.Min(result.Min.Y, b.Min.Y), Math.Min(result.Min.Z, b.Min.Z));
            result.Max = new XYZ(Math.Max(result.Max.X, b.Max.X), Math.Max(result.Max.Y, b.Max.Y), Math.Max(result.Max.Z, b.Max.Z));
        }

        return result;
    }
}
