using Newtonsoft.Json;
using RevitMCPSDK.API.Interfaces;
using System.ComponentModel;

namespace revit_mcp_plugin.Configuration
{
    /// <summary>
    /// <para>Command configuration class.</para>
    /// </summary>
    public class CommandConfig : INotifyPropertyChanged
    {
        /// <summary>
        /// Сообщает окну настроек, что изменилась галочка Enabled: флажки обновляются сразу,
        /// без перерисовки списка (раскрытые категории остаются раскрытыми).
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _enabled = true;
        /// <summary>
        /// <para>Name of the command. Corresponds to <see cref="IRevitCommand.CommandName"/></para>
        /// </summary>
        [JsonProperty("commandName")]
        public string CommandName { get; set; }

        /// <summary>
        /// <para>Assembly path - DLL containing this command.</para>
        /// </summary>
        [JsonProperty("assemblyPath")]
        public string AssemblyPath { get; set; }

        /// <summary>
        /// <para>Enable this command.</para>
        /// </summary>
        [JsonProperty("enabled")]
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
            }
        }

        /// <summary>
        /// <para>Supported Revit versions.</para>
        /// </summary>
        [JsonProperty("supportedRevitVersions")]
        public string[] SupportedRevitVersions { get; set; } = new string[0];

        /// <summary>
        /// <para>Developer information.</para>
        /// </summary>
        [JsonProperty("developer")]
        public DeveloperInfo Developer { get; set; } = new DeveloperInfo();

        /// <summary>
        /// <para>Command description.</para>
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; } = "";

        /// <summary>
        /// Категория команды для окна настроек (название раздела из sections.json). В реестр не пишется.
        /// </summary>
        [JsonIgnore]
        public string Category { get; set; } = "";

        /// <summary>Порядок категории в окне настроек (как в sections.json). В реестр не пишется.</summary>
        [JsonIgnore]
        public int CategoryOrder { get; set; } = int.MaxValue;

        /// <summary>
        /// Входные параметры команды для окна настроек: «template, savePath, overwrite».
        /// Берутся из поля parameters манифеста. В реестр не пишется.
        /// </summary>
        [JsonIgnore]
        public string ParametersText { get; set; } = "";
    }
}
