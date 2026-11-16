using System;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media.Imaging;



namespace revit_mcp_plugin.Core
{
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            RibbonPanel mcpPanel = application.CreateRibbonPanel("Revit MCP Plugin");

            PushButtonData pushButtonData = new PushButtonData("ID_EXCMD_TOGGLE_REVIT_MCP", "Revit MCP\r\n Switch",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.MCPServiceConnection");
            pushButtonData.ToolTip = "Open / Close mcp server";
            pushButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/icon-16.png", UriKind.RelativeOrAbsolute));
            pushButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/icon-32.png", UriKind.RelativeOrAbsolute));
            // Без класса доступности Revit выключает кнопку, пока не открыт документ.
            pushButtonData.AvailabilityClassName = typeof(AlwaysAvailable).FullName;
            mcpPanel.AddItem(pushButtonData);

            PushButtonData mcp_settings_pushButtonData = new PushButtonData("ID_EXCMD_MCP_SETTINGS", "Settings",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.Settings");
            mcp_settings_pushButtonData.ToolTip = "MCP Settings";
            mcp_settings_pushButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-16.png", UriKind.RelativeOrAbsolute));
            mcp_settings_pushButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-32.png", UriKind.RelativeOrAbsolute));
            mcp_settings_pushButtonData.AvailabilityClassName = typeof(AlwaysAvailable).FullName;
            mcpPanel.AddItem(mcp_settings_pushButtonData);

            // Автозапуск сервера: только если его попросили для этого процесса Revit
            // (launch_revit ставит переменную REVIT_MCP_AUTOSTART) или включили в настройках
            // (settings.autoStart в commandRegistry.json). Revit, открытый вручную для
            // обычной работы, порт сам не открывает.
            application.ControlledApplication.ApplicationInitialized += OnApplicationInitialized;

            return Result.Succeeded;
        }

        private static void OnApplicationInitialized(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            var logger = new revit_mcp_plugin.Utils.Logger();
            try
            {
                if (!AutoStartRequested(logger)) return;
                if (!(sender is Autodesk.Revit.ApplicationServices.Application revit)) return;

                SocketService service = SocketService.Instance;
                if (service.IsRunning) return;

                service.Initialize(new UIApplication(revit), "autostart");
                service.Start();
                if (service.IsRunning)
                    logger.Info($"Автозапуск: сервер MCP слушает порт {service.Port}");
                else
                    logger.Error("Автозапуск: сервер MCP не запустился, нет свободного порта");
            }
            catch (Exception ex)
            {
                // Ошибка автозапуска не должна мешать запуску Revit: сервер можно включить кнопкой.
                logger.Error("Автозапуск сервера MCP не удался: " + ex.Message);
            }
        }

        private static bool AutoStartRequested(revit_mcp_plugin.Utils.Logger logger)
        {
            string flag = Environment.GetEnvironmentVariable(SocketService.AutoStartVariable);
            if (!string.IsNullOrEmpty(flag))
            {
                return flag == "1" || flag.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            var config = new revit_mcp_plugin.Configuration.ConfigurationManager(logger);
            config.LoadConfiguration();
            return config.Config?.Settings?.AutoStart == true;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (SocketService.Instance.IsRunning)
                {
                    SocketService.Instance.Stop();
                }
            }
            catch { }

            return Result.Succeeded;
        }
    }
}
