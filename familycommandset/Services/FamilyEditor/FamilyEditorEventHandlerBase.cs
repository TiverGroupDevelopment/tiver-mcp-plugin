using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Models.Common;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public abstract class FamilyEditorEventHandlerBase : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
    private JObject _arguments = new JObject();
    private FailureSuppressor _failures = new FailureSuppressor();

    public AIResult<object> Result { get; private set; }

    public bool TaskCompleted { get; private set; }

    public void Prepare(JObject arguments)
    {
        _arguments = arguments ?? new JObject();
        Result = null;
        TaskCompleted = false;
        _resetEvent.Reset();
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 100000)
    {
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public void Execute(UIApplication app)
    {
        // Ловит ошибки вне транзакции Edit: EditFamily, LoadFamily в проект и т.п.
        _failures = new FailureSuppressor();
        var failures = _failures;
        EventHandler<FailuresProcessingEventArgs> failuresHandler = (sender, e) =>
            e.SetProcessingResult(failures.Process(e.GetFailuresAccessor()));
        app.Application.FailuresProcessing += failuresHandler;

        try
        {
            Result = new AIResult<object> { Success = true, Message = "OK", Response = Run(app, _arguments) };
        }
        catch (Exception ex)
        {
            Result = new AIResult<object> { Success = false, Message = Flatten(ex) };
        }
        finally
        {
            app.Application.FailuresProcessing -= failuresHandler;
            TaskCompleted = true;
            _resetEvent.Set();
        }
    }

    public abstract string GetName();

    protected abstract object Run(UIApplication app, JObject args);

    protected static object Read(UIApplication app, JObject args, Func<FamilyEditSession, object> reader)
    {
        using var session = FamilyEditSession.Open(app, JsonArgs.OptionalString(args, "familyName"));
        return new { family = session.FamilyName, pinnedSession = session.IsPinned, result = reader(session) };
    }

    protected object Edit(UIApplication app, JObject args, string transactionName, Func<Document, object> action)
    {
        using var session = FamilyEditSession.Open(app, JsonArgs.OptionalString(args, "familyName"));
        object result;

        using (var transaction = new Transaction(session.Document, transactionName))
        {
            var options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(_failures);
            transaction.SetFailureHandlingOptions(options);
            transaction.Start();
            result = action(session.Document);
            var status = transaction.Commit();
            if (status != TransactionStatus.Committed || _failures.HasErrors)
            {
                throw new InvalidOperationException($"Транзакция '{transactionName}' не зафиксирована: {status}{DescribeErrors()}");
            }
        }

        session.LoadIntoProject(JsonArgs.OptionalBool(args, "overwriteProjectValues", true));
        if (_failures.HasErrors)
        {
            throw new InvalidOperationException($"Семейство '{session.FamilyName}' изменено, но не загружено в проект{DescribeErrors()}");
        }

        return new
        {
            family = session.FamilyName,
            pinnedSession = session.IsPinned,
            loadedIntoProject = session.IsDetached,
            warnings = _failures.Warnings,
            result
        };
    }

    /// <summary>Предупреждения Revit, собранные за время команды (для обработчиков без Edit).</summary>
    protected IReadOnlyList<string> Warnings => _failures.Warnings;

    protected bool HasFailureErrors => _failures.HasErrors;

    protected string FailureErrors => DescribeErrors();

    private string DescribeErrors()
    {
        return _failures.HasErrors ? $". Revit отменил изменения: {string.Join("; ", _failures.Errors.Take(3))}" : string.Empty;
    }

    private static string Flatten(Exception ex)
    {
        var messages = new List<string>();
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (!messages.Contains(current.Message)) messages.Add(current.Message);
        }

        return string.Join(" → ", messages);
    }
}
