using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils.FamilyEditor;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.FamilyEditor;

public abstract class FamilyEditorEventHandlerBase : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
    private JObject _arguments = new JObject();

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
            TaskCompleted = true;
            _resetEvent.Set();
        }
    }

    public abstract string GetName();

    protected abstract object Run(UIApplication app, JObject args);

    protected static object Read(UIApplication app, JObject args, Func<FamilyEditSession, object> reader)
    {
        using var session = FamilyEditSession.Open(app, JsonArgs.OptionalString(args, "familyName"));
        return new { family = session.FamilyName, result = reader(session) };
    }

    protected static object Edit(UIApplication app, JObject args, string transactionName, Func<Document, object> action)
    {
        using var session = FamilyEditSession.Open(app, JsonArgs.OptionalString(args, "familyName"));
        var collector = new WarningCollector();
        object result;

        using (var transaction = new Transaction(session.Document, transactionName))
        {
            var options = transaction.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(collector);
            transaction.SetFailureHandlingOptions(options);
            transaction.Start();
            result = action(session.Document);
            var status = transaction.Commit();
            if (status != TransactionStatus.Committed)
            {
                throw new InvalidOperationException($"Транзакция '{transactionName}' не зафиксирована: {status}");
            }
        }

        session.LoadIntoProject(JsonArgs.OptionalBool(args, "overwriteProjectValues", true));

        return new
        {
            family = session.FamilyName,
            loadedIntoProject = session.IsDetached,
            warnings = collector.Warnings,
            result
        };
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
