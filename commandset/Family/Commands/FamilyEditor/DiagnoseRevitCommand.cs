using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Models.Common;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

/// <summary>
/// Состояние Revit для разбора сбоев: открытые окна читаются сразу (Win32), остальное — через ExternalEvent.
/// Если открыто модальное окно, главный поток занят и сведения из API недоступны — команда сразу это сообщает.
/// </summary>
public class DiagnoseRevitCommand : FamilyEditorCommandBase
{
    public DiagnoseRevitCommand(UIApplication uiApp)
        : base(new DiagnoseRevitEventHandler(), uiApp)
    {
    }

    public override string CommandName => "diagnose_revit";

    public override object Execute(JObject parameters, string requestId)
    {
        var dialogs = RevitDialogs.Find();
        if (dialogs.Count > 0)
        {
            return new AIResult<object>
            {
                Success = true,
                Message = "Revit заблокирован модальным окном: сведения из API недоступны, пока окно открыто. Закройте его командой dismiss_revit_dialog.",
                Response = new { blocked = true, dialogs = dialogs.Select(d => d.Describe()).ToList() }
            };
        }

        return base.Execute(parameters, requestId);
    }
}
