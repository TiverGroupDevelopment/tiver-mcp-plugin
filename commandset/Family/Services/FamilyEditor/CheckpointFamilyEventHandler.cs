using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Контрольная точка: копия текущего состояния семейства во временной папке.
/// Документ сессии после этого открыт из этой копии; исходный файл не затрагивается до save.
/// </summary>
public class CheckpointFamilyEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Checkpoint family";

    protected override object Run(UIApplication app, JObject args)
    {
        var session = FamilySessionRegistry.Current
                      ?? throw new InvalidOperationException("Контрольные точки работают только в сессии: откройте семейство через new_family или open_family");

        if (session.Origin == "active")
        {
            throw new InvalidOperationException(
                "Сессия закреплена за документом, открытым в интерфейсе Revit: контрольная точка перенесла бы его во временную папку. Закройте сессию и откройте семейство через open_family с path.");
        }

        var checkpoint = session.AddCheckpoint(JsonArgs.OptionalString(args, "label"));
        return new
        {
            checkpoint = checkpoint.Describe(),
            session = session.Describe(),
            warnings = Warnings
        };
    }
}
