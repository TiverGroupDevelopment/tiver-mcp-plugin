using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils.FamilyEditor;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Topography;

public abstract class TopographyEventHandlerBase : IExternalEventHandler, IWaitableExternalEventHandler
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
            var doc = app.ActiveUIDocument?.Document ?? throw new InvalidOperationException("Нет активного документа");
            if (doc.IsFamilyDocument)
            {
                throw new InvalidOperationException("Команды рельефа работают только в документе проекта, активен документ семейства");
            }

            Result = new AIResult<object> { Success = true, Message = "OK", Response = Run(doc, _arguments) };
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

    protected abstract object Run(Document doc, JObject args);

    protected static object Edit(Document doc, string transactionName, Func<object> action)
    {
        var collector = new WarningCollector();
        object result;

        using (var transaction = new Transaction(doc, transactionName))
        {
            Configure(transaction, collector);
            transaction.Start();
            result = action();
            Commit(transaction, transactionName);
        }

        return new { warnings = collector.Warnings, result };
    }

    /// <summary>
    /// Изменение точек TopographySurface возможно только внутри TopographyEditScope, который открывается вне транзакции.
    /// </summary>
    protected static object EditSurfacePoints(Document doc, TopographySurface surface, string transactionName, Func<object> action)
    {
        var collector = new WarningCollector();
        object result;

        using (var scope = new TopographyEditScope(doc, transactionName))
        {
            scope.Start(surface.Id);
            try
            {
                using (var transaction = new Transaction(doc, transactionName))
                {
                    Configure(transaction, collector);
                    transaction.Start();
                    result = action();
                    Commit(transaction, transactionName);
                }

                scope.Commit(collector);
            }
            catch
            {
                if (scope.IsActive) scope.Cancel();
                throw;
            }
        }

        return new { warnings = collector.Warnings, result };
    }

    private static void Configure(Transaction transaction, WarningCollector collector)
    {
        var options = transaction.GetFailureHandlingOptions();
        options.SetFailuresPreprocessor(collector);
        transaction.SetFailureHandlingOptions(options);
    }

    private static void Commit(Transaction transaction, string transactionName)
    {
        var status = transaction.Commit();
        if (status != TransactionStatus.Committed)
        {
            throw new InvalidOperationException($"Транзакция '{transactionName}' не зафиксирована: {status}");
        }
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
