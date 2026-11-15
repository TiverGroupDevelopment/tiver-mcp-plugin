using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPSDK.API.Base;

namespace RevitMCPFamilyCommandSet.Commands.FamilyEditor;

public abstract class FamilyEditorCommandBase : ExternalEventCommandBase
{
    private const int TimeoutMilliseconds = 100000;
    private static readonly object ExecutionLock = new object();

    protected FamilyEditorCommandBase(FamilyEditorEventHandlerBase handler, UIApplication uiApp)
        : base(handler, uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (ExecutionLock)
        {
            var handler = (FamilyEditorEventHandlerBase)Handler;
            handler.Prepare(parameters);
            if (!RaiseAndWaitForCompletion(TimeoutMilliseconds))
            {
                var dialogs = RevitDialogs.DescribeOpen();
                var hint = string.IsNullOrEmpty(dialogs)
                    ? "Открытых окон Revit не найдено: Revit занят другой операцией."
                    : $"Revit ждёт ответа в окне {dialogs}. Закройте его (dismiss_revit_dialog) — команда может выполниться после этого, проверьте состояние перед повтором.";
                throw new TimeoutException($"{CommandName}: Revit не ответил за {TimeoutMilliseconds / 1000} с. {hint}");
            }

            return handler.Result;
        }
    }
}
