using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class AddFamilySharedParametersEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Add family shared parameters";

    protected override object Run(UIApplication app, JObject args)
    {
        var specs = JsonArgs.RequiredArray(args, "parameters").OfType<JObject>().ToList();
        if (specs.Count == 0) throw new ArgumentException("parameters пуст");

        var file = JsonArgs.OptionalString(args, "sharedParameterFile");
        var application = app.Application;
        var previousFile = application.SharedParametersFilename;

        try
        {
            if (file != null)
            {
                var fullPath = Path.GetFullPath(file);
                if (!File.Exists(fullPath)) throw new ArgumentException($"Файл общих параметров '{fullPath}' не найден");
                application.SharedParametersFilename = fullPath;
            }

            var definitionFile = application.OpenSharedParameterFile()
                                 ?? throw new InvalidOperationException(
                                     "В Revit не задан файл общих параметров. Передайте sharedParameterFile или укажите его в «Управление → Общие параметры».");

            var definitions = new List<ExternalDefinition>();
            foreach (DefinitionGroup group in definitionFile.Groups)
            {
                foreach (Definition definition in group.Definitions)
                {
                    if (definition is ExternalDefinition external) definitions.Add(external);
                }
            }

            return Edit(app, args, "MCP: add shared family parameters", doc =>
            {
                var manager = doc.FamilyManager;
                var added = new List<object>();

                foreach (var spec in specs)
                {
                    var definition = FindDefinition(definitions, spec, definitionFile.Filename);
                    if (manager.Parameters.Cast<FamilyParameter>().Any(p => string.Equals(p.Definition.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new ArgumentException($"В семействе уже есть параметр '{definition.Name}'");
                    }

                    var parameter = FamilyParameterSpecs.AddShared(
                        manager,
                        definition,
                        JsonArgs.OptionalString(spec, "group") ?? "Data",
                        JsonArgs.OptionalBool(spec, "isInstance", false));

                    added.Add(new
                    {
                        name = parameter.Definition.Name,
                        guid = definition.GUID.ToString(),
                        kind = FamilyParameterSpecs.Classify(parameter).ToString(),
                        isInstance = parameter.IsInstance,
                        definitionGroup = definition.OwnerGroup?.Name
                    });
                }

                return new { sharedParameterFile = definitionFile.Filename, added };
            });
        }
        finally
        {
            // Настройку пользователя не меняем: возвращаем прежний файл общих параметров.
            if (file != null)
            {
                try
                {
                    application.SharedParametersFilename = previousFile;
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private static ExternalDefinition FindDefinition(List<ExternalDefinition> definitions, JObject spec, string fileName)
    {
        var guidText = JsonArgs.OptionalString(spec, "guid");
        if (guidText != null)
        {
            if (!Guid.TryParse(guidText, out var guid)) throw new ArgumentException($"Некорректный GUID '{guidText}'");
            return definitions.FirstOrDefault(d => d.GUID == guid)
                   ?? throw new ArgumentException($"Общий параметр с GUID {guid} не найден в '{fileName}'");
        }

        var name = JsonArgs.RequiredString(spec, "name");
        var definitionGroup = JsonArgs.OptionalString(spec, "definitionGroup");
        var matches = definitions
            .Where(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))
            .Where(d => definitionGroup == null || string.Equals(d.OwnerGroup?.Name, definitionGroup, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1) return matches[0];
        if (matches.Count == 0)
        {
            throw new ArgumentException($"Общий параметр '{name}' не найден в '{fileName}'");
        }

        throw new ArgumentException(
            $"Общий параметр '{name}' есть в нескольких группах ({string.Join(", ", matches.Select(d => d.OwnerGroup?.Name))}). Укажите definitionGroup или guid.");
    }
}
