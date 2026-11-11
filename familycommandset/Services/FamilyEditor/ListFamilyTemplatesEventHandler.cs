using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class ListFamilyTemplatesEventHandler : FamilyEditorEventHandlerBase
{
    private const int Limit = 300;

    // Проверка шаблона открывает его как документ, поэтому число ограничено.
    private const int InspectLimit = 15;

    public override string GetName() => "List family templates";

    protected override object Run(UIApplication app, JObject args)
    {
        var filter = JsonArgs.OptionalString(args, "filter");
        var category = JsonArgs.OptionalString(args, "category");
        var inspect = JsonArgs.OptionalBool(args, "inspect", false) || category != null;

        var root = FamilyFiles.TemplateRoot(app.Application);
        var templates = FamilyFiles.ListTemplates(root);
        if (filter != null)
        {
            templates = templates.Where(t => t.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        if (!inspect)
        {
            return new
            {
                root,
                count = templates.Count,
                truncated = templates.Count > Limit,
                templates = templates.Take(Limit).ToList()
            };
        }

        if (templates.Count > InspectLimit)
        {
            throw new ArgumentException(
                $"Под фильтр попало {templates.Count} шаблонов, а проверка категории открывает каждый. Сузьте filter так, чтобы осталось не больше {InspectLimit}.");
        }

        var inspected = templates.Select(relative => Inspect(app, root, relative)).ToList();
        if (category != null)
        {
            inspected = inspected
                .Where(t => (t.Category ?? string.Empty).IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0
                            || (t.BuiltInCategory ?? string.Empty).IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        return new
        {
            root,
            count = inspected.Count,
            templates = inspected.Select(t => new
            {
                template = t.Template,
                category = t.Category,
                builtInCategory = t.BuiltInCategory,
                placementType = t.PlacementType,
                hosting = t.Hosting,
                error = t.Error
            }).ToList()
        };
    }

    private static TemplateInfo Inspect(UIApplication app, string root, string relative)
    {
        var info = new TemplateInfo { Template = relative };
        Document doc = null;
        try
        {
            doc = app.Application.NewFamilyDocument(Path.Combine(root, relative));
            var family = doc.OwnerFamily;
            var category = family?.FamilyCategory;
            info.Category = category?.Name;
            if (category != null)
            {
                var value = category.Id.GetValue();
                if (value < 0 && Enum.IsDefined(typeof(BuiltInCategory), (int)value))
                {
                    info.BuiltInCategory = ((BuiltInCategory)(int)value).ToString();
                }
            }

            info.PlacementType = family?.FamilyPlacementType.ToString();
            info.Hosting = family?.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR)?.AsValueString();
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }
        finally
        {
            try
            {
                doc?.Close(false);
            }
            catch (Exception)
            {
            }
        }

        return info;
    }

    private sealed class TemplateInfo
    {
        public string Template { get; set; }
        public string Category { get; set; }
        public string BuiltInCategory { get; set; }
        public string PlacementType { get; set; }
        public string Hosting { get; set; }
        public string Error { get; set; }
    }
}
