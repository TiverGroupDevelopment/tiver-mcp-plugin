using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Models.Common;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

/// <summary>
/// Закрывает модальное окно Revit, которое держит команду до таймаута (ошибка надстройки, «Сохранить?» и т.п.).
/// Работает в потоке сокета без ExternalEvent: пока окно открыто, главный поток Revit занят им.
/// </summary>
public class DismissRevitDialogCommand : IRevitCommand
{
    public DismissRevitDialogCommand(UIApplication uiApp)
    {
    }

    public string CommandName => "dismiss_revit_dialog";

    public object Execute(JObject parameters, string requestId)
    {
        parameters ??= new JObject();
        var listOnly = parameters["listOnly"]?.Value<bool?>() ?? false;
        var all = parameters["all"]?.Value<bool?>() ?? false;
        var button = parameters["button"]?.Value<string>();
        var handle = parameters["handle"]?.Value<long?>();

        var before = RevitDialogs.Find();
        if (listOnly || before.Count == 0)
        {
            return new AIResult<object>
            {
                Success = true,
                Message = before.Count == 0 ? "Открытых окон Revit нет" : $"Открыто окон: {before.Count}",
                Response = new { dialogs = before.Select(d => d.Describe()).ToList(), clicked = new object[0] }
            };
        }

        var clicked = new List<object>();
        var anyClicked = false;
        var attempts = all ? 10 : 1;
        for (var i = 0; i < attempts; i++)
        {
            var (ok, pressed, dialog) = RevitDialogs.Click(i == 0 ? handle : null, button);
            if (dialog == null) break;
            clicked.Add(new { dialog = dialog.Describe(), button = pressed, clicked = ok });
            anyClicked |= ok;
            if (!ok) break;
            Thread.Sleep(700);
        }

        Thread.Sleep(500);
        var after = RevitDialogs.Find();
        return new AIResult<object>
        {
            Success = anyClicked,
            Message = anyClicked
                ? $"Нажато кнопок: {clicked.Count}. Осталось окон: {after.Count}"
                : $"Кнопка {(button ?? "Отмена/Закрыть/ОК")} не найдена. Доступные: {string.Join(", ", before.SelectMany(d => d.Buttons).Distinct())}",
            Response = new { clicked, remaining = after.Select(d => d.Describe()).ToList() }
        };
    }
}
