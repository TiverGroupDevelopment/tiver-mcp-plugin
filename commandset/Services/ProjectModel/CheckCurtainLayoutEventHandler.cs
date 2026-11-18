using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.ProjectModel;

namespace RevitMCPCommandSet.Services.ProjectModel;

/// <summary>
/// Проверки раскладки витражей: максимальная длина импостов и совпадение
/// вертикальных осей с другим витражом (например, экран перед витражом). Только читает модель.
/// </summary>
public class CheckCurtainLayoutEventHandler : ProjectModelEventHandlerBase
{
    public override string GetName() => "Check curtain layout";

    protected override object Run(UIApplication app, JObject args)
    {
        var doc = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var walls = ProjectModelUtils.ResolveWalls(doc, args);
        var tolerance = JsonArgs.OptionalDouble(args, "toleranceMm", 1);
        var checks = new List<object>();
        var failedIds = new HashSet<long>();
        var anyFailed = false;

        if (args["maxMullionLength"] is JObject maxRule)
        {
            var maxMm = JsonArgs.RequiredDouble(maxRule, "maxMm");
            var typeFilter = JsonArgs.OptionalString(maxRule, "mullionType");
            var violations = new List<object>();
            var checkedCount = 0;
            foreach (var wall in walls)
            {
                foreach (var m in CurtainWallReader.Mullions(wall))
                {
                    if (typeFilter != null && !string.Equals(m.Type, typeFilter, StringComparison.OrdinalIgnoreCase)) continue;
                    checkedCount++;
                    if (m.LengthMm > maxMm + tolerance)
                    {
                        violations.Add(new { wall = wall.Id.GetValue(), mullion = m.Id, type = m.Type, lengthMm = m.LengthMm });
                        failedIds.Add(m.Id);
                    }
                }
            }

            if (violations.Count > 0) anyFailed = true;
            checks.Add(new
            {
                check = "maxMullionLength",
                rule = $"длина импостов{(typeFilter != null ? $" '{typeFilter}'" : "")} ≤ {maxMm} мм",
                status = checkedCount == 0 ? "warn" : violations.Count == 0 ? "pass" : "fail",
                checkedMullions = checkedCount,
                violations
            });
        }

        if (args["alignWith"] is JObject alignRule)
        {
            var reference = ProjectModelUtils.ResolveWalls(doc, alignRule);
            var refLines = new List<(XYZ point, Wall wall)>();
            foreach (var wall in reference)
            {
                var axis = CurtainWallReader.Axis(wall);
                refLines.AddRange(CurtainWallReader.VerticalGridPositions(wall).Select(s => (axis.origin + axis.direction * s, wall)));
            }

            var results = new List<object>();
            foreach (var wall in walls)
            {
                var axis = CurtainWallReader.Axis(wall);
                // Оси эталона, попадающие в длину проверяемого витража (в проекции на его ось).
                var targets = refLines.Select(r => (r.point - axis.origin).DotProduct(axis.direction))
                    .Where(s => s > tolerance / ProjectModelUtils.MmPerFoot && s < axis.length - tolerance / ProjectModelUtils.MmPerFoot)
                    .Distinct().OrderBy(s => s).ToList();
                var own = CurtainWallReader.VerticalGridPositions(wall);
                var tol = tolerance / ProjectModelUtils.MmPerFoot;
                var missing = targets.Where(t => !own.Any(o => Math.Abs(o - t) <= tol)).Select(ProjectModelUtils.Mm).ToList();
                results.Add(new
                {
                    wall = wall.Id.GetValue(),
                    referenceLinesInRange = targets.Count,
                    matched = targets.Count - missing.Count,
                    missingAtAlongWallMm = missing
                });
                if (missing.Count > 0) failedIds.Add(wall.Id.GetValue());
            }

            var failed = failedIds.Any(id => walls.Any(w => w.Id.GetValue() == id));
            if (failed) anyFailed = true;
            checks.Add(new
            {
                check = "alignWith",
                rule = "вертикальные оси эталонных витражей совпадают с осями проверяемых",
                status = failed ? "fail" : "pass",
                walls = results
            });
        }

        if (checks.Count == 0) throw new ArgumentException("Задайте хотя бы одну проверку: maxMullionLength или alignWith");

        var uiDoc = app.ActiveUIDocument;
        if (failedIds.Count > 0 && uiDoc != null && uiDoc.Document.Equals(doc) && JsonArgs.OptionalBool(args, "selectFailed", true))
        {
            uiDoc.Selection.SetElementIds(failedIds.Select(FamilyEditorIds.FromNumber).ToList());
        }

        return new
        {
            project = doc.Title,
            walls = walls.Select(w => w.Id.GetValue()).ToList(),
            toleranceMm = tolerance,
            status = anyFailed ? "fail" : "pass",
            checks,
            selectedFailed = failedIds.Count
        };
    }
}
