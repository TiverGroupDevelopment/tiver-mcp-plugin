using System.Windows;

namespace revit_mcp_plugin.UI
{
    /// <summary>
    /// Окно настроек: одна страница — наборы команд.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            ContentFrame.Navigate(new CommandSetSettingsPage());
        }
    }
}
