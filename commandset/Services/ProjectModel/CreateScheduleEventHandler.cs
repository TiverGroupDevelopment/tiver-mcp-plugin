using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPCommandSet.Services.ProjectModel;

/// <summary>Спецификация по категории: поля, группировка, итоги; возвращает строки таблицы для сверки.</summary>
public class CreateScheduleEventHandler : ProjectModelEventHandlerBase
{
    public override string GetName() => "Create schedule";

    protected override object Run(UIApplication app, JObject args)
    {
        var doc = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var categoryName = JsonArgs.RequiredString(args, "category");
        if (!Enum.TryParse(categoryName, true, out BuiltInCategory category)) throw new ArgumentException($"Категория '{categoryName}': ожидается OST_…, например OST_CurtainWallMullions");
        var name = JsonArgs.RequiredString(args, "name");
        var fieldNames = JsonArgs.RequiredArray(args, "fields").Select(t => t.Value<string>()).ToList();
        var groupBy = (args["groupBy"] as JArray)?.Select(t => t.Value<string>()).ToList() ?? new List<string>();
        var itemized = JsonArgs.OptionalBool(args, "itemized", false);
        var open = JsonArgs.OptionalBool(args, "open", false);

        var schedule = InTransaction(doc, $"MCP: спецификация {name}", failures =>
        {
            var old = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>().FirstOrDefault(v => v.Name == name);
            if (old != null) doc.Delete(old.Id);

            var vs = ViewSchedule.CreateSchedule(doc, new ElementId(category));
            vs.Name = name;
            var definition = vs.Definition;
            var available = definition.GetSchedulableFields();
            var added = new Dictionary<string, ScheduleFieldId>(StringComparer.OrdinalIgnoreCase);
            foreach (var fieldName in fieldNames)
            {
                var field = FindField(doc, available, fieldName)
                            ?? throw new ArgumentException($"Поле '{fieldName}' недоступно. Есть: {string.Join(", ", available.Select(f => f.GetName(doc)).Distinct().Take(60))}");
                added[fieldName] = definition.AddField(field).FieldId;
            }

            foreach (var g in groupBy)
            {
                if (!added.TryGetValue(g, out var fieldId)) throw new ArgumentException($"groupBy '{g}' нет среди fields");
                definition.AddSortGroupField(new ScheduleSortGroupField(fieldId));
            }

            definition.IsItemized = itemized;
            definition.ShowGrandTotal = true;
            definition.ShowGrandTotalCount = true;
            return vs;
        });

        var body = schedule.GetTableData().GetSectionData(SectionType.Body);
        var rows = new List<List<string>>();
        for (var r = 0; r < body.NumberOfRows; r++)
        {
            var row = new List<string>();
            for (var c = 0; c < body.NumberOfColumns; c++) row.Add(schedule.GetCellText(SectionType.Body, r, c));
            if (row.Any(cell => !string.IsNullOrWhiteSpace(cell))) rows.Add(row);
        }

        if (open && app.ActiveUIDocument != null && app.ActiveUIDocument.Document.Equals(doc)) app.ActiveUIDocument.RequestViewChange(schedule);

        return new { project = doc.Title, id = schedule.Id.GetValue(), name = schedule.Name, rows };
    }

    private static SchedulableField FindField(Document doc, IList<SchedulableField> fields, string name)
    {
        if (string.Equals(name, "count", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Количество", StringComparison.OrdinalIgnoreCase))
        {
            var count = fields.FirstOrDefault(f => f.FieldType == ScheduleFieldType.Count);
            if (count != null) return count;
        }

        if (Enum.TryParse(name, true, out BuiltInParameter builtIn))
        {
            var byId = fields.FirstOrDefault(f => f.ParameterId == new ElementId(builtIn));
            if (byId != null) return byId;
        }

        return fields.FirstOrDefault(f => string.Equals(f.GetName(doc), name, StringComparison.OrdinalIgnoreCase));
    }
}
