using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Models.JsonRPC;
using RevitMCPSDK.API.Interfaces;
using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Utils;

namespace revit_mcp_plugin.Core
{
    public class SocketService
    {
        private static SocketService _instance;
        private TcpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private const int DefaultPort = 8080;
        private const int PortRange = 10;

        /// <summary>Переменная окружения процесса Revit: «запусти сервер сам при старте» (1/true).</summary>
        public const string AutoStartVariable = "REVIT_MCP_AUTOSTART";
        /// <summary>Переменная окружения процесса Revit: базовый порт сервера для этого процесса.</summary>
        public const string PortVariable = "REVIT_MCP_PORT";
        private int _basePort = DefaultPort;
        private int _port = DefaultPort;
        private UIApplication _uiApp;
        private RevitCommandRegistry _commandRegistry;
        private ILogger _logger;
        private CommandExecutor _commandExecutor;

        // Встроенный метод: отвечает с потока сокета без обращения к Revit API,
        // поэтому работает, даже когда Revit занят или ждёт ответа в диалоге.
        public const string StatusMethod = "mcp_status";
        private string _revitVersion = "";
        private string _startedBy = "";
        private string[] _loadProblems = new string[0];

        public static SocketService Instance
        {
            get
            {
                if(_instance == null)
                    _instance = new SocketService();
                return _instance;
            }
        }

        private SocketService()
        {
            _commandRegistry = new RevitCommandRegistry();
            _logger = new Logger();
        }

        public bool IsRunning => _isRunning;

        public int Port
        {
            get => _port;
            set => _port = value;
        }

        // Initialization.
        /// <param name="uiApp">Приложение Revit.</param>
        /// <param name="startedBy">Кто запустил сервер: "button" (кнопка на ленте) или "autostart".</param>
        public void Initialize(UIApplication uiApp, string startedBy = "button")
        {
            _uiApp = uiApp;
            _startedBy = startedBy;

            // Initialize ExternalEventManager
            ExternalEventManager.Instance.Initialize(uiApp, _logger);

            // Get the current Revit version.
            var versionAdapter = new RevitMCPSDK.API.Utils.RevitVersionAdapter(_uiApp.Application);
            string currentVersion = versionAdapter.GetRevitVersion();
            _revitVersion = currentVersion;
            _logger.Info("Current Revit version: {0}", currentVersion);



            // Create CommandExecutor
            _commandExecutor = new CommandExecutor(_commandRegistry, _logger);

            // Load configuration and register commands.
            ConfigurationManager configManager = new ConfigurationManager(_logger);
            configManager.LoadConfiguration();
            

            // Base port from configuration (default 8080). The actual port may be
            // higher if the base port is taken by another Revit instance - see Start().
            _basePort = configManager.Config?.Settings?.Port > 0
                ? configManager.Config.Settings.Port
                : DefaultPort;

            // Порт для этого процесса Revit задаёт тот, кто его запустил (launch_revit):
            // так он заранее знает, где ждать ответ.
            if (int.TryParse(Environment.GetEnvironmentVariable(PortVariable), out int requestedPort) && requestedPort > 0)
            {
                _basePort = requestedPort;
            }
            _port = _basePort;

            // Load command.
            // Сервер включают и выключают кнопкой много раз за сеанс: реестр чистится,
            // чтобы повторная загрузка не выглядела как дубли.
            _commandRegistry.ClearCommands();
            CommandManager commandManager = new CommandManager(
                _commandRegistry, _logger, configManager, _uiApp);
            commandManager.LoadCommands();
            _loadProblems = commandManager.Problems.ToArray();

            _logger.Info($"Socket service initialized on port {_port}");
        }

        public void Start()
        {
            if (_isRunning) return;

            // Try base port, then the next ones, so several Revit instances can run side by side.
            for (int port = _basePort; port < _basePort + PortRange; port++)
            {
                TcpListener listener = null;
                try
                {
                    // Только локальный адрес: среди команд есть выполнение произвольного кода
                    // (send_code_to_revit), из сети порт недоступен.
                    listener = new TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                }
                catch (SocketException)
                {
                    listener?.Stop();
                    _logger?.Info($"Port {port} is busy, trying next");
                    continue;
                }

                _listener = listener;
                _port = port;
                _isRunning = true;

                _listenerThread = new Thread(ListenForClients)
                {
                    IsBackground = true
                };
                _listenerThread.Start();
                _logger?.Info($"Socket service listening on port {_port}");
                return;
            }

            _isRunning = false;
            _logger?.Error($"No free port in range {_basePort}-{_basePort + PortRange - 1}");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            try
            {
                _isRunning = false;

                _listener?.Stop();
                _listener = null;

                if(_listenerThread!=null && _listenerThread.IsAlive)
                {
                    _listenerThread.Join(1000);
                }
            }
            catch (Exception)
            {
                // log error
            }
        }

        private void ListenForClients()
        {
            try
            {
                while (_isRunning)
                {
                    TcpClient client = _listener.AcceptTcpClient();

                    Thread clientThread = new Thread(HandleClientCommunication)
                    {
                        IsBackground = true
                    };
                    clientThread.Start(client);
                }
            }
            catch (SocketException)
            {
                
            }
            catch(Exception)
            {
                // log
            }
        }

        private void HandleClientCommunication(object clientObj)
        {
            TcpClient tcpClient = (TcpClient)clientObj;
            NetworkStream stream = tcpClient.GetStream();

            try
            {
                byte[] buffer = new byte[8192];
                // Сервер не разделяет сообщения, а TCP режет длинные запросы на куски:
                // копим байты, пока не соберётся целый JSON-объект.
                var pending = new MemoryStream();

                while (_isRunning && tcpClient.Connected)
                {
                    // Read client messages.
                    int bytesRead = 0;

                    try
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                    }
                    catch (IOException)
                    {
                        // Client disconnected.
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        // Client disconnected.
                        break;
                    }

                    pending.Write(buffer, 0, bytesRead);
                    int messageLength = FindCompleteJsonLength(pending.GetBuffer(), (int)pending.Length);
                    if (messageLength < 0)
                    {
                        continue;
                    }

                    string message = Encoding.UTF8.GetString(pending.GetBuffer(), 0, messageLength);
                    var rest = (int)pending.Length - messageLength;
                    var tail = new byte[rest];
                    Array.Copy(pending.GetBuffer(), messageLength, tail, 0, rest);
                    pending = new MemoryStream();
                    pending.Write(tail, 0, rest);
                    System.Diagnostics.Trace.WriteLine($"Received message: {message}");

                    string response = ProcessJsonRPCRequest(message);

                    // Send response.
                    byte[] responseData = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseData, 0, responseData.Length);
                }
            }
            catch(Exception)
            {
                // log
            }
            finally
            {
                tcpClient.Close();
            }
        }

        /// <summary>
        /// Длина в байтах первого целого JSON-объекта в буфере или -1, если он ещё не дочитан.
        /// Байты UTF-8 продолжения (>= 0x80) не совпадают со скобками и кавычками, поэтому
        /// разбор по байтам безопасен.
        /// </summary>
        private static int FindCompleteJsonLength(byte[] data, int length)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            bool started = false;

            for (int i = 0; i < length; i++)
            {
                byte b = data[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (b == (byte)'\\') escaped = true;
                    else if (b == (byte)'"') inString = false;
                    continue;
                }

                if (b == (byte)'"') inString = true;
                else if (b == (byte)'{' || b == (byte)'[') { depth++; started = true; }
                else if (b == (byte)'}' || b == (byte)']')
                {
                    depth--;
                    if (started && depth == 0) return i + 1;
                }
            }

            return -1;
        }

        private string ProcessJsonRPCRequest(string requestJson)
        {
            JsonRPCRequest request;

            try
            {
                // Parse JSON-RPC requests.
                request = JsonConvert.DeserializeObject<JsonRPCRequest>(requestJson);

                // Verify that the request format is valid.
                if (request == null || !request.IsValid())
                {
                    return CreateErrorResponse(
                        null,
                        JsonRPCErrorCodes.InvalidRequest,
                        "Invalid JSON-RPC request"
                    );
                }

                if (request.Method == StatusMethod)
                {
                    return CreateSuccessResponse(request.Id, GetStatus());
                }

                // Search for the command in the registry.
                if (!_commandRegistry.TryGetCommand(request.Method, out var command))
                {
                    return CreateErrorResponse(request.Id, JsonRPCErrorCodes.MethodNotFound,
                        $"Method '{request.Method}' not found");
                }

                // Execute command.
                try
                {                
                    object result = command.Execute(request.GetParamsObject(), request.Id);

                    return CreateSuccessResponse(request.Id, result);
                }
                catch (Exception ex)
                {
                    return CreateErrorResponse(request.Id, JsonRPCErrorCodes.InternalError, ex.Message);
                }
            }
            catch (JsonException)
            {
                // JSON parsing error.
                return CreateErrorResponse(
                    null,
                    JsonRPCErrorCodes.ParseError,
                    "Invalid JSON"
                );
            }
            catch (Exception ex)
            {
                // Catch other errors produced when processing requests.
                return CreateErrorResponse(
                    null,
                    JsonRPCErrorCodes.InternalError,
                    $"Internal error: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Состояние сервера без обращения к Revit API: версия Revit, порт, процесс,
        /// число загруженных команд и проблемы загрузки (дубли имён, отсутствующие сборки).
        /// </summary>
        private object GetStatus()
        {
            var commands = new System.Collections.Generic.List<string>(_commandRegistry.GetRegisteredCommands());
            commands.Sort(StringComparer.Ordinal);
            return new
            {
                plugin = "tiver-mcp",
                revitVersion = _revitVersion,
                port = _port,
                processId = System.Diagnostics.Process.GetCurrentProcess().Id,
                startedBy = _startedBy,
                commandCount = commands.Count,
                commands,
                loadProblems = _loadProblems
            };
        }

        private string CreateSuccessResponse(string id, object result)
        {
            var response = new JsonRPCSuccessResponse
            {
                Id = id,
                Result = result is JToken jToken ? jToken : JToken.FromObject(result)
            };

            return response.ToJson();
        }

        private string CreateErrorResponse(string id, int code, string message, object data = null)
        {
            var response = new JsonRPCErrorResponse
            {
                Id = id,
                Error = new JsonRPCError
                {
                    Code = code,
                    Message = message,
                    Data = data != null ? JToken.FromObject(data) : null
                }
            };

            return response.ToJson();
        }
    }
}
