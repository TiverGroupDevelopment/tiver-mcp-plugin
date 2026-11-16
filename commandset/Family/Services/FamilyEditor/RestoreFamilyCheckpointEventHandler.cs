using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using System.IO;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>Откат к контрольной точке: текущий документ закрывается без сохранения, открывается её копия.</summary>
public class RestoreFamilyCheckpointEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Restore family checkpoint";

    protected override object Run(UIApplication app, JObject args)
    {
        var session = FamilySessionRegistry.Current
                      ?? throw new InvalidOperationException("Нет открытой сессии семейства");
        if (session.Checkpoints.Count == 0)
        {
            throw new InvalidOperationException("В сессии нет контрольных точек (checkpoint_family)");
        }

        var checkpoint = JsonArgs.Has(args, "id")
            ? session.Checkpoints.FirstOrDefault(c => c.Id == args["id"].Value<int>())
              ?? throw new ArgumentException(
                  $"Контрольная точка {args["id"]} не найдена. Доступные: {string.Join(", ", session.Checkpoints.Select(c => c.Id))}")
            : session.Checkpoints[session.Checkpoints.Count - 1];

        if (!File.Exists(checkpoint.Path))
        {
            throw new InvalidOperationException($"Файл контрольной точки {checkpoint.Id} не найден: {checkpoint.Path}");
        }

        var current = session.Document;
        var active = app.ActiveUIDocument?.Document;
        if (active != null && active.Equals(current))
        {
            throw new InvalidOperationException("Документ семейства активен в интерфейсе Revit: API не может закрыть его для отката. Активируйте другой документ.");
        }

        if (!current.Close(false))
        {
            throw new InvalidOperationException("Revit не закрыл текущий документ семейства, откат не выполнен");
        }

        Autodesk.Revit.DB.Document restored;
        try
        {
            restored = app.Application.OpenDocumentFile(checkpoint.Path)
                       ?? throw new InvalidOperationException("Revit вернул пустой документ");
        }
        catch (Exception ex)
        {
            // Старый документ уже закрыт, сессия потеряна: даём путь, по которому состояние можно поднять вручную.
            FamilySessionRegistry.Unpin();
            throw new InvalidOperationException(
                $"Текущий документ закрыт, но контрольная точка не открылась: {ex.Message}. Сессия сброшена; откройте файл через open_family: {checkpoint.Path}",
                ex);
        }

        session.Rebind(restored);

        return new
        {
            restored = checkpoint.Describe(),
            session = session.Describe(),
            document = FamilyDocInfo.Summarize(restored),
            warnings = Warnings
        };
    }
}
