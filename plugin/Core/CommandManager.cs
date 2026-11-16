using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPSDK.API.Utils;
using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// <para>Command Manager</para>
    /// </summary>
    public class CommandManager
    {
        private readonly ICommandRegistry _commandRegistry;
        private readonly ILogger _logger;
        private readonly ConfigurationManager _configManager;
        private readonly UIApplication _uiApplication;
        private readonly RevitVersionAdapter _versionAdapter;

        // Имя команды -> сборка, из которой она загружена. Нужен, чтобы второй набор
        // с тем же именем команды не подменил первую молча.
        private readonly Dictionary<string, string> _loadedFrom =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Что пошло не так при загрузке: дубли имён, отсутствующие сборки и команды.
        /// Отдаётся клиенту в ответе mcp_status.
        /// </summary>
        public List<string> Problems { get; } = new List<string>();

        /// <summary>
        /// Manager in charge of loading and managing commands.
        /// </summary>
        /// <param name="commandRegistry"></param>
        /// <param name="logger"></param>
        /// <param name="configManager"></param>
        /// <param name="uiApplication"></param>
        public CommandManager(
            ICommandRegistry commandRegistry,
            ILogger logger,
            ConfigurationManager configManager,
            UIApplication uiApplication)
        {
            _commandRegistry = commandRegistry;
            _logger = logger;
            _configManager = configManager;
            _uiApplication = uiApplication;
            _versionAdapter = new RevitVersionAdapter(_uiApplication.Application);
        }

        /// <summary>
        /// <para>Load all commands specified in the configuration file.</para>
        /// </summary>
        public void LoadCommands()
        {
            _logger.Info("Start loading command.");
            _loadedFrom.Clear();
            Problems.Clear();
            string currentVersion = _versionAdapter.GetRevitVersion();
            _logger.Info("Current Revit version: {0}", currentVersion);

            // Load external commands from the configuration file.
            foreach (var commandConfig in _configManager.Config.Commands)
            {
                try
                {
                    if (!commandConfig.Enabled)
                    {
                        _logger.Info("Skipping disabled command: {0}", commandConfig.CommandName);
                        continue;
                    }

                    // Check Revit version compatibility.
                    if (commandConfig.SupportedRevitVersions != null &&
                        commandConfig.SupportedRevitVersions.Length > 0 &&
                        !_versionAdapter.IsVersionSupported(commandConfig.SupportedRevitVersions))
                    {
                        _logger.Warning("The command {0} is not supported by the current Revit version ({1}) and it has been skipped.",
                            commandConfig.CommandName, currentVersion);
                        continue;
                    }

                    // Имя команды должно быть уникальным во всех наборах: реестр плоский.
                    // Первая загруженная команда остаётся, дубль не загружается и попадает в лог.
                    if (_loadedFrom.TryGetValue(commandConfig.CommandName ?? "", out var firstAssembly))
                    {
                        Report($"Дубль имени команды '{commandConfig.CommandName}': уже загружена из {firstAssembly}, запись из {commandConfig.AssemblyPath} пропущена.");
                        continue;
                    }

                    // Replace version placeholder strings in paths.
                    commandConfig.AssemblyPath = commandConfig.AssemblyPath.Contains("{VERSION}")
                        ? commandConfig.AssemblyPath.Replace("{VERSION}", currentVersion)
                        : commandConfig.AssemblyPath;

                    // Load external command assembly.
                    if (LoadCommandFromAssembly(commandConfig))
                    {
                        _loadedFrom[commandConfig.CommandName] = commandConfig.AssemblyPath;
                    }
                    else
                    {
                        Report($"Команда '{commandConfig.CommandName}' не загружена из {commandConfig.AssemblyPath}: нет сборки или в ней нет класса с таким CommandName.");
                    }
                }
                catch (Exception ex)
                {
                    Problems.Add($"Команда '{commandConfig.CommandName}' не загружена: {ex.Message}");
                    _logger.Error("Failed to load command {0}: {1}", commandConfig.CommandName, ex.Message);
                }
            }

            _logger.Info("Command loading complete.");
        }

        /// <summary>
        /// Loads specific commands in specific assemblies.
        /// </summary>
        /// <param name="config">Configuration class describing the command.</param>
        private void Report(string problem)
        {
            Problems.Add(problem);
            _logger.Error(problem);
        }

        /// <returns>true, если команда с именем из конфигурации найдена в сборке и зарегистрирована.</returns>
        private bool LoadCommandFromAssembly(CommandConfig config)
        {
            try
            {
                // Determine the assembly path.
                string assemblyPath = config.AssemblyPath;
                if (!Path.IsPathRooted(assemblyPath))
                {
                    // If it is not an absolute path, then it is relative to the Command's directory.
                    string baseDir = PathManager.GetCommandsDirectoryPath();
                    assemblyPath = Path.Combine(baseDir, assemblyPath);
                }

                if (!File.Exists(assemblyPath))
                {
                    _logger.Error("Command assembly does not exist: {0}", assemblyPath);
                    return false;
                }

                // Load assembly.
                Assembly assembly = Assembly.LoadFrom(assemblyPath);

                // Find types that implement the IRevitCommand interface.
                foreach (Type type in assembly.GetTypes())
                {
                    if (typeof(RevitMCPSDK.API.Interfaces.IRevitCommand).IsAssignableFrom(type) &&
                        !type.IsInterface &&
                        !type.IsAbstract)
                    {
                        try
                        {
                            // Create a command instance.
                            RevitMCPSDK.API.Interfaces.IRevitCommand command;

                            // Check whether the command implements the initializable interface.
                            if (typeof(IRevitCommandInitializable).IsAssignableFrom(type))
                            {
                                // Create instance and initialize.
                                command = (IRevitCommand)Activator.CreateInstance(type);
                                ((IRevitCommandInitializable)command).Initialize(_uiApplication);
                            }
                            else
                            {
                                // Try searching for constructors that accept UIApplication.
                                var constructor = type.GetConstructor(new[] { typeof(UIApplication) });
                                if (constructor != null)
                                {
                                    command = (IRevitCommand)constructor.Invoke(new object[] { _uiApplication });
                                }
                                else
                                {
                                    // Use a parameterless constructor.
                                    command = (IRevitCommand)Activator.CreateInstance(type);
                                }
                            }

                            // Check whether the command name matches the configuration.
                            if (command.CommandName == config.CommandName)
                            {
                                _commandRegistry.RegisterCommand(command);
                                _logger.Info("Registered command [{0}]: {1}",
                                    command.CommandName, Path.GetFileName(assemblyPath));
                                return true; // Exit after finding a matching command.
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Failed to create command instance [{0}]: {1}", type.FullName, ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to load command assembly: {0}", ex.Message);
            }

            return false;
        }
    }
}
