using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.ProjectModel;

namespace RevitMCPCommandSet.Services.ProjectModel;

/// <summary>Копирует тип (если задан duplicateAs) и записывает параметры типа по имени.</summary>
public class EditElementTypeEventHandler : ProjectModelEventHandlerBase
{
    public override string GetName() => "Edit element type";

    protected override object Run(UIApplication app, JObject args)
    {
        var doc = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var typeName = JsonArgs.RequiredString(args, "type");
        var kind = JsonArgs.OptionalString(args, "kind");
        var duplicateAs = JsonArgs.OptionalString(args, "duplicateAs");
        var parameters = args["parameters"] as JObject ?? new JObject();

        return InTransaction(doc, $"MCP: тип {duplicateAs ?? typeName}", failures =>
        {
            var source = ProjectModelUtils.FindType(doc, typeName, kind);
            var target = source;
            var duplicated = false;
            if (duplicateAs != null)
            {
                var existing = new FilteredElementCollector(doc).OfClass(source.GetType()).Cast<Element>().OfType<ElementType>()
                    .FirstOrDefault(t => string.Equals(t.Name, duplicateAs, StringComparison.OrdinalIgnoreCase)
                                         && string.Equals(t.FamilyName, source.FamilyName, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    target = existing;
                }
                else
                {
                    target = source.Duplicate(duplicateAs);
                    duplicated = true;
                }
            }

            var applied = new List<object>();
            var errors = new List<string>();
            foreach (var property in parameters.Properties())
            {
                try
                {
                    ProjectModelUtils.SetParameter(doc, target, property.Name, property.Value);
                    doc.Regenerate();
                    applied.Add(ProjectModelUtils.Describe(doc, ProjectModelUtils.FindParameter(target, property.Name)));
                }
                catch (Exception ex)
                {
                    errors.Add($"{property.Name}: {ex.Message}");
                }
            }

            if (errors.Count > 0) throw new ArgumentException($"Параметры не заданы, изменения отменены: {string.Join(" | ", errors)}");

            return new
            {
                project = doc.Title,
                id = target.Id.GetValue(),
                type = ProjectModelUtils.FullName(target),
                @class = target.GetType().Name,
                duplicatedFrom = duplicated ? ProjectModelUtils.FullName(source) : null,
                reusedExisting = duplicateAs != null && !duplicated,
                parameters = applied,
                warnings = failures.Warnings
            };
        });
    }
}
