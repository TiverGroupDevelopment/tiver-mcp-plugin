using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Проверка семейства «на изгиб»: для каждого набора значений параметров пробует применить их,
/// собирает ошибки и предупреждения Revit и размеры получившихся форм, затем откатывает изменения.
/// Семейство после проверки остаётся в исходном состоянии.
/// </summary>
public class TestFamilyFlexEventHandler : FamilyEditorEventHandlerBase
{
    private const int MaxCases = 50;

    public override string GetName() => "Test family flex";

    protected override object Run(UIApplication app, JObject args)
    {
        var cases = JsonArgs.RequiredArray(args, "cases").OfType<JObject>().ToList();
        if (cases.Count == 0) throw new ArgumentException("cases пуст");
        if (cases.Count > MaxCases) throw new ArgumentException($"Слишком много наборов: {cases.Count}, максимум {MaxCases}");

        return Read(app, args, session =>
        {
            var doc = session.Document;
            var results = new List<object>();
            var failed = 0;

            for (var index = 0; index < cases.Count; index++)
            {
                var result = RunCase(doc, cases[index], index);
                if (!result.Ok) failed++;
                results.Add(result.Report);
            }

            return new
            {
                total = cases.Count,
                passed = cases.Count - failed,
                failed,
                cases = results
            };
        });
    }

    private static (bool Ok, object Report) RunCase(Document doc, JObject spec, int index)
    {
        var name = JsonArgs.OptionalString(spec, "name") ?? $"case {index + 1}";
        var values = spec["values"] as JObject ?? new JObject();
        var typeName = JsonArgs.OptionalString(spec, "type");
        var manager = doc.FamilyManager;
        var failures = new FailureSuppressor();
        string error = null;
        object forms = null;

        using (var group = new TransactionGroup(doc, "MCP: flex " + name))
        {
            group.Start();
            try
            {
                using (var transaction = new Transaction(doc, "MCP: flex values"))
                {
                    var options = transaction.GetFailureHandlingOptions();
                    options.SetFailuresPreprocessor(failures);
                    transaction.SetFailureHandlingOptions(options);
                    transaction.Start();

                    if (typeName != null) manager.CurrentType = FamilyParameterSpecs.FindType(manager, typeName);
                    foreach (var pair in values)
                    {
                        FamilyParameterSpecs.SetCurrentTypeValue(doc, manager, FamilyParameterSpecs.Find(manager, pair.Key), pair.Value);
                    }

                    doc.Regenerate();
                    var status = transaction.Commit();
                    if (status != TransactionStatus.Committed && !failures.HasErrors)
                    {
                        error = $"Транзакция не зафиксирована: {status}";
                    }
                }

                if (error == null && !failures.HasErrors)
                {
                    forms = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .ToElements()
                        .OfType<GenericForm>()
                        .Select(form =>
                        {
                            var box = form.get_BoundingBox(null);
                            return new
                            {
                                id = form.Id.GetValue(),
                                kind = form.GetType().Name,
                                isSolid = form.IsSolid,
                                volumeCm3 = FamilyGeometryReader.VolumeCm3(form),
                                sizeMm = box == null ? null : FamilyDocInfo.Mm(box.Max - box.Min)
                            };
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                // Откат всей группы: проверка не оставляет следов в семействе.
                group.RollBack();
            }
        }

        var ok = error == null && !failures.HasErrors;
        return (ok, new
        {
            name,
            type = typeName,
            values,
            ok,
            error,
            errors = failures.Errors,
            warnings = failures.Warnings,
            forms
        });
    }
}
