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
        private int _basePort = DefaultPort;
        private int _port = DefaultPort;
        private UIApplication _uiApp;
        private ICommandRegistry _commandRegistry;
        private ILogger _logger;
        private CommandExecutor _commandExecutor;

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

        // 初始化
        // Initialization.
        public void Initialize(UIApplication uiApp)
        {
            _uiApp = uiApp;

            // 初始化事件管理器
            // Initialize ExternalEventManager
            ExternalEventManager.Instance.Initialize(uiApp, _logger);

            // 记录当前 Revit 版本
            // Get the current Revit version.
            var versionAdapter = new RevitMCPSDK.API.Utils.RevitVersionAdapter(_uiApp.Application);
            string currentVersion = versionAdapter.GetRevitVersion();
            _logger.Info("当前 Revit 版本: {0}\nCurrent Revit version: {0}", currentVersion);



            // 创建命令执行器
            // Create CommandExecutor
            _commandExecutor = new CommandExecutor(_commandRegistry, _logger);

            // 加载配置并注册命令
            // Load configuration and register commands.
            ConfigurationManager configManager = new ConfigurationManager(_logger);
            configManager.LoadConfiguration();
            

            // Base port from configuration (default 8080). The actual port may be
            // higher if the base port is taken by another Revit instance - see Start().
            _basePort = configManager.Config?.Settings?.Port > 0
                ? configManager.Config.Settings.Port
                : DefaultPort;
            _port = _basePort;

            // 加载命令
            // Load command.
            CommandManager commandManager = new CommandManager(
                _commandRegistry, _logger, configManager, _uiApp);
            commandManager.LoadCommands();

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
                    listener = new TcpListener(IPAddress.Any, port);
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
                    // 读取客户端消息
                    // Read client messages.
                    int bytesRead = 0;

                    try
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                    }
                    catch (IOException)
                    {
                        // 客户端断开连接
                        // Client disconnected.
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        // 客户端断开连接
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
                    System.Diagnostics.Trace.WriteLine($"收到消息: {message}\nReceived message: {message}");

                    string response = ProcessJsonRPCRequest(message);

                    // 发送响应
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
                // 解析JSON-RPC请求
                // Parse JSON-RPC requests.
                request = JsonConvert.DeserializeObject<JsonRPCRequest>(requestJson);

                // 验证请求格式是否有效
                // Verify that the request format is valid.
                if (request == null || !request.IsValid())
                {
                    return CreateErrorResponse(
                        null,
                        JsonRPCErrorCodes.InvalidRequest,
                        "Invalid JSON-RPC request"
                    );
                }

                // 查找命令
                // Search for the command in the registry.
                if (!_commandRegistry.TryGetCommand(request.Method, out var command))
                {
                    return CreateErrorResponse(request.Id, JsonRPCErrorCodes.MethodNotFound,
                        $"Method '{request.Method}' not found");
                }

                // 执行命令
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
                // JSON解析错误
                // JSON parsing error.
                return CreateErrorResponse(
                    null,
                    JsonRPCErrorCodes.ParseError,
                    "Invalid JSON"
                );
            }
            catch (Exception ex)
            {
                // 处理请求时的其他错误
                // Catch other errors produced when processing requests.
                return CreateErrorResponse(
                    null,
                    JsonRPCErrorCodes.InternalError,
                    $"Internal error: {ex.Message}"
                );
            }
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
