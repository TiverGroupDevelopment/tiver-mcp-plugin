using Newtonsoft.Json;

namespace revit_mcp_plugin.Configuration
{
    /// <summary>
    /// <para>Service settings.</para>
    /// </summary>
    public class ServiceSettings
    {
        /// <summary>
        /// <para>Log level.</para>
        /// </summary>
        [JsonProperty("logLevel")]
        public string LogLevel { get; set; } = "Info";

        /// <summary>
        /// <para>Socket service port.</para>
        /// </summary>
        [JsonProperty("port")]
        public int Port { get; set; } = 8080;

        /// <summary>
        /// <para>Запускать сервер при старте Revit без кнопки. По умолчанию выключено:
        /// сервер поднимается только для Revit, запущенного с переменной REVIT_MCP_AUTOSTART=1.</para>
        /// <para>Start the server when Revit starts. Off by default.</para>
        /// </summary>
        [JsonProperty("autoStart")]
        public bool AutoStart { get; set; } = false;

    }
}
