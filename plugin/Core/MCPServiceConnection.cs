using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// Кнопка Revit MCP Switch. Сервер не запущен — запускает его и показывает порт.
    /// Сервер уже работает (например, после автозапуска) — показывает порт и спрашивает,
    /// остановить ли его: случайное нажатие не должно выключать сервер, с которым работает агент.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class MCPServiceConnection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                SocketService service = SocketService.Instance;

                if (service.IsRunning)
                {
                    var dialog = new TaskDialog("Revit MCP")
                    {
                        MainInstruction = $"Сервер MCP работает на порту {service.Port}",
                        MainContent = "Агент подключается к нему по этому порту. Остановить сервер?",
                        CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                        DefaultButton = TaskDialogResult.No
                    };
                    if (dialog.Show() == TaskDialogResult.Yes)
                    {
                        service.Stop();
                        TaskDialog.Show("Revit MCP", "Сервер MCP остановлен.");
                    }
                }
                else
                {
                    service.Initialize(commandData.Application);
                    service.Start();
                    if (service.IsRunning)
                        TaskDialog.Show("Revit MCP", $"Сервер MCP запущен на порту {service.Port}.");
                    else
                        TaskDialog.Show("Revit MCP", "Сервер MCP не запустился: нет свободного порта (8080–8089).");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
