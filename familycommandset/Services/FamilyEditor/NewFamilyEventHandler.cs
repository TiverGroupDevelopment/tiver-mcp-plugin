using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class NewFamilyEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "New family";

    protected override object Run(UIApplication app, JObject args)
    {
        var existing = FamilySessionRegistry.Current;
        if (existing != null)
        {
            throw new InvalidOperationException($"Уже открыта сессия семейства '{existing.FamilyName}'. Сначала вызовите close_family.");
        }

        var template = FamilyFiles.ResolveTemplate(app.Application, JsonArgs.RequiredString(args, "template"));
        var savePath = JsonArgs.OptionalString(args, "savePath");
        string fullSavePath = null;
        var overwritten = false;
        if (savePath != null)
        {
            fullSavePath = FamilyFiles.ValidateSavePath(savePath, JsonArgs.OptionalBool(args, "overwrite", false), out overwritten);
        }

        var doc = app.Application.NewFamilyDocument(template)
                  ?? throw new InvalidOperationException($"Revit не создал семейство по шаблону '{template}'");
        try
        {
            // Многие шаблоны (типовая модель, оборудование) приходят без типоразмеров, и тогда
            // команды параметров падают. Создаём типоразмер сразу.
            string createdType = null;
            if (doc.FamilyManager.Types.Size == 0)
            {
                createdType = JsonArgs.OptionalString(args, "typeName")
                              ?? (fullSavePath != null ? System.IO.Path.GetFileNameWithoutExtension(fullSavePath) : "Тип 1");
                using (var transaction = new Transaction(doc, "MCP: типоразмер по умолчанию"))
                {
                    transaction.Start();
                    doc.FamilyManager.NewType(createdType);
                    transaction.Commit();
                }
            }

            if (fullSavePath != null)
            {
                FamilyFiles.SaveAsReplacing(doc, fullSavePath, overwritten);
            }

            var session = FamilySessionRegistry.Pin(doc, null, "new");
            return new
            {
                session = session.Describe(),
                template,
                savedTo = fullSavePath,
                createdType,
                document = FamilyDocInfo.Summarize(doc),
                warnings = Warnings
            };
        }
        catch (Exception)
        {
            try
            {
                doc.Close(false);
            }
            catch (Exception)
            {
            }

            throw;
        }
    }
}
