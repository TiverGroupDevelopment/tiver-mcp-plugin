using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using Autodesk.Revit.DB.Events;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Commands.ExecuteDynamicCode
{
    /// <summary>
    /// 处理代码执行的外部事件处理器
    /// </summary>
    public class ExecuteCodeEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public const string TransactionModeAuto = "auto";
        public const string TransactionModeNone = "none";

        // 代码执行参数
        private string _generatedCode;
        private object[] _executionParameters;
        private string _transactionMode = TransactionModeAuto;
        private bool _suppressFailures = true;

        // 执行结果信息
        public ExecutionResultInfo ResultInfo { get; private set; }

        // 状态同步对象
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        // 设置要执行的代码和参数
        public void SetExecutionParameters(string code, object[] parameters = null, string transactionMode = TransactionModeAuto, bool suppressFailures = true)
        {
            _generatedCode = code;
            _executionParameters = parameters ?? Array.Empty<object>();
            _transactionMode = transactionMode == TransactionModeNone ? TransactionModeNone : TransactionModeAuto;
            _suppressFailures = suppressFailures;
            TaskCompleted = false;
            _resetEvent.Reset();
        }

        // 等待执行完成 - IWaitableExternalEventHandler接口实现
        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            ResultInfo = new ExecutionResultInfo();
            var suppressor = _suppressFailures ? new FailureSuppressor() : null;
            EventHandler<FailuresProcessingEventArgs> failuresHandler = null;

            try
            {
                var doc = app.ActiveUIDocument.Document;

                // Транзакции, открытые самим кодом (в т.ч. в семействах через EditFamily), ловим событием приложения
                if (suppressor != null)
                {
                    failuresHandler = (sender, e) =>
                        e.SetProcessingResult(suppressor.Process(e.GetFailuresAccessor()));
                    app.Application.FailuresProcessing += failuresHandler;
                }

                object result;
                if (_transactionMode == TransactionModeNone)
                {
                    result = CompileAndExecuteCode(
                        code: _generatedCode,
                        doc: doc,
                        parameters: _executionParameters
                    );
                }
                else
                {
                    using (var transaction = new Transaction(doc, "执行AI代码"))
                    {
                        if (suppressor != null)
                        {
                            var options = transaction.GetFailureHandlingOptions();
                            options.SetFailuresPreprocessor(suppressor);
                            transaction.SetFailureHandlingOptions(options);
                        }

                        transaction.Start();

                        result = CompileAndExecuteCode(
                            code: _generatedCode,
                            doc: doc,
                            parameters: _executionParameters
                        );

                        var status = transaction.Commit();
                        if (status != TransactionStatus.Committed)
                        {
                            throw new InvalidOperationException($"Транзакция не зафиксирована: {status}");
                        }
                    }
                }

                if (suppressor != null && suppressor.HasErrors)
                {
                    throw new InvalidOperationException("Revit отменил изменения из-за ошибок");
                }

                ResultInfo.Success = true;
                ResultInfo.Result = JsonConvert.SerializeObject(result);
            }
            catch (Exception ex)
            {
                ResultInfo.Success = false;
                ResultInfo.ErrorMessage = $"执行失败: {ex.Message}";
                if (suppressor != null && suppressor.HasErrors)
                {
                    ResultInfo.ErrorMessage += $" Revit сообщил: {string.Join("; ", suppressor.Errors.Take(3))}";
                }
            }
            finally
            {
                if (failuresHandler != null)
                {
                    app.Application.FailuresProcessing -= failuresHandler;
                }

                if (suppressor != null)
                {
                    ResultInfo.Warnings = suppressor.Warnings;
                    ResultInfo.Failures = suppressor.Errors;
                }

                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        private object CompileAndExecuteCode(string code, Document doc, object[] parameters)
        {
            // 包装代码以规范入口点
            var wrappedCode = $@"
using System;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace AIGeneratedCode
{{
    public static class CodeExecutor
    {{
        public static object Execute(Document document, object[] parameters)
        {{
            // 用户代码入口
            {code}
        }}
    }}
}}";

            Assembly assembly;
            try
            {
                assembly = CompileWithRoslyn(wrappedCode);
            }
            catch (Exception ex) when (IsRoslynLoadFailure(ex))
            {
                // Другие надстройки (особенно в Revit 2019) загружают несовместимые
                // System.Memory / System.Runtime.CompilerServices.Unsafe, из-за чего Roslyn
                // не инициализируется. Встроенный в .NET Framework компилятор CodeDom
                // (C# 5) от этих сборок не зависит.
                assembly = CompileWithCodeDom(wrappedCode);
            }

            var executorType = assembly.GetType("AIGeneratedCode.CodeExecutor");
            var executeMethod = executorType.GetMethod("Execute");

            return executeMethod.Invoke(null, new object[] { doc, parameters });
        }

        private static bool IsRoslynLoadFailure(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is TypeInitializationException || e is FileLoadException ||
                    e is FileNotFoundException || e is MissingMethodException ||
                    e is TypeLoadException || e is BadImageFormatException)
                    return true;
            }
            return false;
        }

        private static IEnumerable<string> LoadedAssemblyPaths()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                {
                    try { return !a.IsDynamic && !string.IsNullOrEmpty(a.Location); }
                    catch { return false; }
                })
                .Select(a => a.Location)
                .GroupBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First());
        }

        private static Assembly CompileWithCodeDom(string wrappedCode)
        {
            using (var provider = new Microsoft.CSharp.CSharpCodeProvider())
            {
                var options = new System.CodeDom.Compiler.CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false,
                    TreatWarningsAsErrors = false
                };
                foreach (var path in LoadedAssemblyPaths())
                {
                    var name = Path.GetFileName(path);
                    // Фасады netstandard и mscorlib компилятор csc подключает сам
                    if (name.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase)) continue;
                    options.ReferencedAssemblies.Add(path);
                }

                var results = provider.CompileAssemblyFromSource(options, wrappedCode);
                var errors = results.Errors.Cast<System.CodeDom.Compiler.CompilerError>()
                    .Where(e => !e.IsWarning)
                    .Select(e => $"Line {e.Line - 14}:{e.ErrorText}")
                    .ToList();
                if (errors.Count > 0)
                {
                    throw new Exception($"代码编译错误 (CodeDom, C# 5):\n{string.Join("\n", errors)}");
                }
                return results.CompiledAssembly;
            }
        }

        private static Assembly CompileWithRoslyn(string wrappedCode)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(wrappedCode);

            // 添加必要的程序集引用（引用所有已加载的程序集）
            var references = LoadedAssemblyPaths()
                .Select(p => MetadataReference.CreateFromFile(p))
                .Cast<MetadataReference>()
                .ToList();

            // 编译代码
            var compilation = CSharpCompilation.Create(
                "AIGeneratedCode",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);

                // 处理编译结果
                if (!result.Success)
                {
                    var errors = string.Join("\n", result.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => $"Line {d.Location.GetLineSpan().StartLinePosition.Line}: {d.GetMessage()}"));
                    throw new Exception($"代码编译错误:\n{errors}");
                }

                ms.Seek(0, SeekOrigin.Begin);
                return Assembly.Load(ms.ToArray());
            }
        }

        public string GetName()
        {
            return "执行AI代码";
        }
    }

    // 执行结果数据结构
    public class ExecutionResultInfo
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("errorMessage")]
        public string ErrorMessage { get; set; } = string.Empty;

        [JsonProperty("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        [JsonProperty("failures")]
        public List<string> Failures { get; set; } = new List<string>();
    }
}
