using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Topography;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Topography;

public abstract class TopographyCommandBase : ExternalEventCommandBase
{
    private const int TimeoutMilliseconds = 100000;
    private static readonly object ExecutionLock = new object();

    protected TopographyCommandBase(TopographyEventHandlerBase handler, UIApplication uiApp)
        : base(handler, uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        lock (ExecutionLock)
        {
            var handler = (TopographyEventHandlerBase)Handler;
            handler.Prepare(parameters);
            if (!RaiseAndWaitForCompletion(TimeoutMilliseconds))
            {
                throw new TimeoutException($"{CommandName}: Revit не ответил за {TimeoutMilliseconds / 1000} с");
            }

            return handler.Result;
        }
    }
}
