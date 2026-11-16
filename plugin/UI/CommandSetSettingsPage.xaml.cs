using Newtonsoft.Json;
using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace revit_mcp_plugin.UI
{
    /// <summary>
    /// Interaction logic for CommandSetSettingsPage.xaml
    /// </summary>
    public partial class CommandSetSettingsPage : Page
    {
        private ObservableCollection<CommandSet> commandSets;
        private ObservableCollection<CommandConfig> currentCommands;
        // Разработчик из манифеста пакета, где лежит команда (для новых записей реестра).
        private readonly Dictionary<string, DeveloperInfo> commandDevelopers = new Dictionary<string, DeveloperInfo>();

        public CommandSetSettingsPage()
        {
            InitializeComponent();
            // Initialize data collections
            commandSets = new ObservableCollection<CommandSet>();
            currentCommands = new ObservableCollection<CommandConfig>();
            // Set data bindings
            CommandSetListBox.ItemsSource = commandSets;
            FeaturesListView.ItemsSource = currentCommands;
            // Load command sets
            LoadCommandSets();
            // Initial state
            NoSelectionTextBlock.Visibility = Visibility.Visible;
            // Ширина окна по столбцам — как только окно покажется.
            Loaded += (sender, e) => FitWindowToColumns();
        }

        private void LoadCommandSets()
        {
            try
            {
                commandSets.Clear();
                string commandsDirectory = PathManager.GetCommandsDirectoryPath();
                string registryFilePath = PathManager.GetCommandRegistryFilePath();
                // 1. First load all command set folders, establish available command collections
                // Папка в Commands — пакет кода (DLL + command.json). Наборы, которые видит пользователь,
                // берутся из поля sets у команды: одна команда может входить в несколько наборов
                // (Global и Alterplast). В разных наборах это один и тот же объект команды:
                // галочка в одном наборе — галочка во всех, в реестр команда попадает один раз.
                Dictionary<string, CommandSet> availableCommandSets = new Dictionary<string, CommandSet>();
                HashSet<string> availableCommandNames = new HashSet<string>();
                Dictionary<string, CommandConfig> commandsByName = new Dictionary<string, CommandConfig>();
                commandDevelopers.Clear();
                // Get all command set directories
                string[] commandSetDirectories = Directory.GetDirectories(commandsDirectory);
                foreach (var directory in commandSetDirectories)
                {
                    // Skip special folders or hidden folders
                    if (Path.GetFileName(directory).StartsWith("."))
                        continue;
                    string commandJsonPath = Path.Combine(directory, "command.json");
                    // If there's a command.json, load it
                    if (File.Exists(commandJsonPath))
                    {
                        string commandJson = File.ReadAllText(commandJsonPath);
                        var commandSetData = JsonConvert.DeserializeObject<CommandJson>(commandJson);
                        if (commandSetData != null)
                        {
                            var categories = LoadSections(directory);
                            // Описания наборов из манифеста; набор по умолчанию — сам пакет.
                            var setDescriptions = (commandSetData.CommandSets ?? new List<CommandSetInfoJson>())
                                .Where(s => !string.IsNullOrEmpty(s.Name))
                                .ToDictionary(s => s.Name, s => s.Description);
                            CommandSet SetFor(string setName)
                            {
                                if (!availableCommandSets.TryGetValue(setName, out var set))
                                {
                                    set = new CommandSet
                                    {
                                        Name = setName,
                                        Description = setDescriptions.TryGetValue(setName, out var description) ? description : commandSetData.Description,
                                        Commands = new List<CommandConfig>()
                                    };
                                    availableCommandSets[setName] = set;
                                }
                                return set;
                            }

                            // Detect supported Revit versions from the year subfolders
                            List<string> supportedVersions = new List<string>();
                            var versionDirectories = Directory.GetDirectories(directory)
                                .Select(Path.GetFileName)
                                .Where(name => int.TryParse(name, out _))
                                .ToList();

                            // Loop through each command
                            foreach (var command in commandSetData.Commands)
                            {
                                // Build a command config; supported versions come from the folders
                                List<string> supportedCommandVersions = new List<string>();
                                string dllBasePath = null;

                                foreach (var version in versionDirectories)
                                {
                                    string versionDirectory = Path.Combine(directory, version);
                                    string versionDllPath = null;

                                    if (!string.IsNullOrEmpty(command.AssemblyPath))
                                    {
                                        // Relative path given: look for it in the version subfolder
                                        versionDllPath = Path.Combine(versionDirectory, command.AssemblyPath);
                                        if (File.Exists(versionDllPath))
                                        {
                                            // Remember the base path template
                                            if (dllBasePath == null)
                                            {
                                                // Build the template from the relative path
                                                dllBasePath = Path.Combine(commandSetData.Name, "{VERSION}", command.AssemblyPath);
                                            }
                                            supportedCommandVersions.Add(version);
                                        }
                                    }
                                    else
                                    {
                                        // No path given: take any DLL in the version subfolder
                                        var dllFiles = Directory.GetFiles(versionDirectory, "*.dll");
                                        if (dllFiles.Length > 0)
                                        {
                                            versionDllPath = dllFiles[0]; // Use the first DLL found
                                            if (dllBasePath == null)
                                            {
                                                // Get the DLL file name
                                                string dllFileName = Path.GetFileName(versionDllPath);
                                                dllBasePath = Path.Combine(commandSetData.Name, "{VERSION}", dllFileName);
                                            }
                                            supportedCommandVersions.Add(version);
                                        }
                                    }
                                }

                                // At least one version supports this command
                                if (supportedCommandVersions.Count > 0 && dllBasePath != null)
                                {
                                    // Команда с тем же именем из другого пакета не загрузится: показываем первую.
                                    if (commandsByName.ContainsKey(command.CommandName)) continue;

                                    // Create the command config
                                    var commandConfig = new CommandConfig
                                    {
                                        CommandName = command.CommandName,
                                        Description = command.Description,
                                        // Path with the version placeholder
                                        AssemblyPath = dllBasePath,
                                        Enabled = false,
                                        // All supported versions
                                        SupportedRevitVersions = supportedCommandVersions.ToArray()
                                    };
                                    commandConfig.ParametersText = command.Parameters == null || command.Parameters.Count == 0
                                        ? "—"
                                        : string.Join(", ", command.Parameters.Select(p => p.Name));
                                    // Категория — раздел команды; название и порядок — из sections.json.
                                    if (!string.IsNullOrEmpty(command.Section) && categories.TryGetValue(command.Section, out var category))
                                    {
                                        commandConfig.Category = category.Title;
                                        commandConfig.CategoryOrder = category.Order;
                                    }
                                    else
                                    {
                                        commandConfig.Category = string.IsNullOrEmpty(command.Section) ? "Без категории" : command.Section;
                                    }
                                    commandsByName[command.CommandName] = commandConfig;
                                    commandDevelopers[command.CommandName] = commandSetData.Developer;
                                    availableCommandNames.Add(command.CommandName);

                                    // Тот же объект — во всех наборах команды.
                                    var setNames = command.Sets != null && command.Sets.Count > 0
                                        ? command.Sets
                                        : new List<string> { commandSetData.Name };
                                    foreach (var setName in setNames.Distinct())
                                    {
                                        SetFor(setName).Commands.Add(commandConfig);
                                    }
                                }
                            }
                        }
                    }
                }
                // 2. Load registry, update command enabled status, and clean up non-existent commands
                if (File.Exists(registryFilePath))
                {
                    string registryJson = File.ReadAllText(registryFilePath);
                    var registry = JsonConvert.DeserializeObject<CommandRegistryJson>(registryJson);
                    if (registry?.Commands != null)
                    {
                        // Keep only valid commands
                        List<CommandConfig> validCommands = new List<CommandConfig>();
                        foreach (var registryItem in registry.Commands)
                        {
                            if (availableCommandNames.Contains(registryItem.CommandName))
                            {
                                validCommands.Add(registryItem);
                                // Update the enabled status of this command in all command sets
                                foreach (var commandSet in availableCommandSets.Values)
                                {
                                    var command = commandSet.Commands.FirstOrDefault(c => c.CommandName == registryItem.CommandName);
                                    if (command != null)
                                    {
                                        command.Enabled = registryItem.Enabled;
                                    }
                                }
                            }
                        }
                        // If there are invalid commands, update the registry file
                        if (validCommands.Count != registry.Commands.Count)
                        {
                            registry.Commands = validCommands;
                            string updatedJson = JsonConvert.SerializeObject(registry, Formatting.Indented);
                            File.WriteAllText(registryFilePath, updatedJson);
                        }
                    }
                }
                // 3. Add command sets to the UI collection
                foreach (var commandSet in availableCommandSets.Values)
                {
                    commandSets.Add(commandSet);
                }
                // If no command sets found, display a message
                if (commandSets.Count == 0)
                {
                    MessageBox.Show("No command sets found. Please check if the Commands folder exists and contains valid command sets.",
                                  "No Command Sets", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading command sets: {ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Разделы (категории) команд: sections.json рядом с command.json пакета. Ключ → название и порядок.
        /// Нет файла — категорией служит сам ключ раздела.
        /// </summary>
        private static Dictionary<string, (string Title, int Order)> LoadSections(string packageDirectory)
        {
            var result = new Dictionary<string, (string Title, int Order)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string path = Path.Combine(packageDirectory, "sections.json");
                if (!File.Exists(path)) return result;
                var sections = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                int order = 0;
                foreach (var property in sections.Properties())
                {
                    result[property.Name] = (property.Value.ToString(), order++);
                }
            }
            catch
            {
                // Испорченный sections.json не должен ломать окно: категории будут ключами разделов.
            }
            return result;
        }

        private void CommandSetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowSelectedCommandSet();
        }

        private void GroupByCategoryCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ShowSelectedCommandSet();
        }

        /// <summary>
        /// Команды выбранного набора: с флажком Category — по раскрывающимся категориям
        /// (в порядке sections.json), без него — одним списком в порядке манифеста.
        /// </summary>
        private void ShowSelectedCommandSet()
        {
            if (currentCommands == null || FeaturesListView == null) return;
            currentCommands.Clear();
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(FeaturesListView.ItemsSource);
            view?.GroupDescriptions.Clear();

            var selectedCommandSet = CommandSetListBox.SelectedItem as CommandSet;
            if (selectedCommandSet == null)
            {
                NoSelectionTextBlock.Visibility = Visibility.Visible;
                FeaturesHeaderTextBlock.Text = "Command List";
                return;
            }

            NoSelectionTextBlock.Visibility = Visibility.Collapsed;
            FeaturesHeaderTextBlock.Text = $"{selectedCommandSet.Name} - Command List ({selectedCommandSet.Commands.Count})";

            bool grouped = GroupByCategoryCheckBox.IsChecked == true;
            // OrderBy устойчивая: внутри категории команды идут в порядке манифеста.
            var commands = grouped
                ? selectedCommandSet.Commands.OrderBy(c => c.CategoryOrder).ToList()
                : selectedCommandSet.Commands;
            foreach (var command in commands)
            {
                currentCommands.Add(command);
            }
            if (grouped)
            {
                view?.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(CommandConfig.Category)));
            }
        }

        // Галочки обновляются сами (CommandConfig сообщает об изменении Enabled):
        // список не перерисовывается, раскрытые категории остаются раскрытыми.

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Only operate on the currently displayed commands
            foreach (var command in currentCommands)
            {
                command.Enabled = true;
            }
        }

        /// <summary>
        /// Select Set: включить все команды открытого набора и выключить все остальные.
        /// Команда, которая входит и в открытый набор, и в другой, остаётся включённой.
        /// </summary>
        private void SelectSetButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedCommandSet = CommandSetListBox.SelectedItem as CommandSet;
            if (selectedCommandSet == null)
            {
                MessageBox.Show("Select a command set on the left first.", "Select Set",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var inSet = new HashSet<CommandConfig>(selectedCommandSet.Commands);
            foreach (var command in commandSets.SelectMany(set => set.Commands).Distinct())
            {
                command.Enabled = inSet.Contains(command);
            }
        }

        private void UnselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            // Only operate on the currently displayed commands
            foreach (var command in currentCommands)
            {
                command.Enabled = false;
            }
        }

        /// <summary>Показать или скрыть столбец Parameters целиком.</summary>
        private void ShowParametersCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (FeaturesGridView == null || ParametersColumn == null) return;
            bool show = ShowParametersCheckBox.IsChecked == true;
            bool shown = FeaturesGridView.Columns.Contains(ParametersColumn);
            if (show && !shown)
            {
                // На своё место: после Name.
                FeaturesGridView.Columns.Insert(Math.Min(2, FeaturesGridView.Columns.Count), ParametersColumn);
            }
            else if (!show && shown)
            {
                FeaturesGridView.Columns.Remove(ParametersColumn);
            }
            FitWindowToColumns();
        }

        /// <summary>
        /// Ширина окна — ровно по столбцам списка команд: правый край Description совпадает с краем списка,
        /// горизонтальной прокрутки нет. Вызывается при открытии окна и при переключении флажка Parameters.
        /// </summary>
        private void FitWindowToColumns()
        {
            // После того как WPF разложит столбцы: иначе ширины ещё старые.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var window = Window.GetWindow(this);
                if (window == null || FeaturesListView.ActualWidth <= 0) return;
                if (window.WindowState != WindowState.Normal) return;

                double columns = FeaturesGridView.Columns.Sum(column => double.IsNaN(column.Width) ? column.ActualWidth : column.Width);
                // Вертикальная прокрутка списка и внутренние отступы строк ListView.
                double needed = columns + SystemParameters.VerticalScrollBarWidth + 12;
                double delta = needed - FeaturesListView.ActualWidth;
                if (Math.Abs(delta) < 1) return;

                double maxWidth = SystemParameters.WorkArea.Width;
                double width = Math.Max(window.MinWidth, Math.Min(maxWidth, window.ActualWidth + delta));
                // Окно не должно уехать за правый край экрана.
                if (window.Left + width > SystemParameters.WorkArea.Right)
                {
                    window.Left = Math.Max(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right - width);
                }
                window.Width = width;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string registryFilePath = PathManager.GetCommandRegistryFilePath();
                // Read the existing registry to keep full command info
                Dictionary<string, CommandConfig> existingCommandsDict = new Dictionary<string, CommandConfig>();
                IDictionary<string, Newtonsoft.Json.Linq.JToken> otherData = null;
                if (File.Exists(registryFilePath))
                {
                    string registryJson = File.ReadAllText(registryFilePath);
                    var existingRegistry = JsonConvert.DeserializeObject<CommandRegistryJson>(registryJson);
                    otherData = existingRegistry?.OtherData;
                    if (existingRegistry?.Commands != null)
                    {
                        foreach (var cmd in existingRegistry.Commands)
                        {
                            existingCommandsDict[cmd.CommandName] = cmd;
                        }
                    }
                }
                // Create a new registry object
                CommandRegistryJson registry = new CommandRegistryJson();
                registry.Commands = new List<CommandConfig>();
                registry.OtherData = otherData;
                // Collect all enabled commands
                // Команда может входить в несколько наборов: в реестр она попадает один раз.
                var saved = new HashSet<string>();
                foreach (var commandSet in commandSets)
                {
                    foreach (var command in commandSet.Commands)
                    {
                        // Only enabled commands go to the registry
                        if (command.Enabled && saved.Add(command.CommandName))
                        {
                            var commandSetDeveloper = commandDevelopers.TryGetValue(command.CommandName, out var developer) && developer != null
                                ? developer
                                : new DeveloperInfo { Name = "Unspecified", Email = "Unspecified" };
                            CommandConfig newConfig;
                            // Check whether the command is already in the previous registry
                            if (existingCommandsDict.ContainsKey(command.CommandName))
                            {
                                // Existing: keep its info, update only enabled state and path template
                                newConfig = existingCommandsDict[command.CommandName];
                                newConfig.Enabled = true;
                                newConfig.AssemblyPath = command.AssemblyPath;
                                newConfig.SupportedRevitVersions = command.SupportedRevitVersions;
                            }
                            else
                            {
                                // New command: create a new config
                                newConfig = new CommandConfig
                                {
                                    CommandName = command.CommandName,
                                    AssemblyPath = command.AssemblyPath ?? "",
                                    Enabled = true,
                                    Description = command.Description,
                                    SupportedRevitVersions = command.SupportedRevitVersions,
                                    Developer = commandSetDeveloper
                                };
                            }
                            registry.Commands.Add(newConfig);
                        }
                    }
                }
                // Build the summary to show
                string enabledFeaturesText = "";
                int enabledCount = registry.Commands.Count;
                foreach (var command in registry.Commands)
                {
                    var setNames = commandSets.Where(cs => cs.Commands.Any(c => c.CommandName == command.CommandName)).Select(cs => cs.Name).ToList();
                    string commandSetName = setNames.Count > 0 ? string.Join(", ", setNames) : "Unknown";
                    string versions = command.SupportedRevitVersions != null && command.SupportedRevitVersions.Any()
                        ? $" (Revit {string.Join(", ", command.SupportedRevitVersions)})"
                        : "";
                    enabledFeaturesText += $"• {commandSetName}: {command.CommandName}\n";
                }
                // Serialize and save to file
                string json = JsonConvert.SerializeObject(registry, Formatting.Indented);
                File.WriteAllText(registryFilePath, json);
                MessageBox.Show($"Command set settings successfully saved!\n\nEnabled {enabledCount} commands:\n{enabledFeaturesText}",
                              "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>Open Logs: папка логов плагина (загрузка команд, порт, ошибки).</summary>
        private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", PathManager.GetLogsDirectoryPath());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Logs folder: {ex.Message}", "Error",
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Data models
    public class CommandSet
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<CommandConfig> Commands { get; set; } = new List<CommandConfig>();
        /// <summary>Число команд набора для списка наборов: « (56)».</summary>
        public string CountText => $" ({Commands.Count})";
    }
    // Configuration files
    public class CommandRegistryJson
    {
        public List<CommandConfig> Commands { get; set; } = new List<CommandConfig>();

        // Other top-level keys of commandRegistry.json (e.g. "settings" with the port).
        // Kept so that saving the command list does not drop them.
        [JsonExtensionData]
        public IDictionary<string, Newtonsoft.Json.Linq.JToken> OtherData { get; set; }
    }

    public class CommandJson
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<CommandItemJson> Commands { get; set; } = new List<CommandItemJson>();
        public DeveloperInfo Developer { get; set; }
        public List<string> SupportedRevitVersions { get; set; } = new List<string>();
        /// <summary>Наборы команд: имя и описание. Состав набора — поле sets у команд.</summary>
        public List<CommandSetInfoJson> CommandSets { get; set; } = new List<CommandSetInfoJson>();
    }

    public class CommandSetInfoJson
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Project { get; set; }
    }

    public class CommandItemJson
    {
        public string CommandName { get; set; }
        public string Description { get; set; }
        public string AssemblyPath { get; set; }
        /// <summary>Наборы, в которые входит команда (Global, Alterplast). Пусто — набор = пакет.</summary>
        public List<string> Sets { get; set; }
        /// <summary>Раздел (категория) команды: ключ из sections.json.</summary>
        public string Section { get; set; }
        /// <summary>Входные параметры команды (scripts/sync-command-parameters.mjs).</summary>
        public List<CommandParameterJson> Parameters { get; set; }
    }

    public class CommandParameterJson
    {
        public string Name { get; set; }
        public bool Required { get; set; }
    }
}