using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class DiagnoseRevitEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Diagnose Revit";

    protected override object Run(UIApplication app, JObject args)
    {
        var application = app.Application;
        var activeDoc = app.ActiveUIDocument?.Document;
        var session = FamilySessionRegistry.Current;

        var documents = application.Documents.Cast<Document>()
            .Select(d => new
            {
                title = d.Title,
                kind = d.IsFamilyDocument ? "family" : (d.IsLinked ? "link" : "project"),
                path = string.IsNullOrEmpty(d.PathName) ? null : d.PathName,
                isModified = d.IsModified,
                isActive = activeDoc != null && d.Equals(activeDoc),
                isPinnedSession = session != null && d.Equals(session.Document),
                category = d.IsFamilyDocument ? d.OwnerFamily?.FamilyCategory?.Name : null
            })
            .ToList();

        var updaters = UpdaterRegistry.GetRegisteredUpdaterInfos()
            .Select(u => new { name = u.UpdaterName, application = u.ApplicationName })
            .OrderBy(u => u.application)
            .ToList();

        return new
        {
            revit = new
            {
                version = application.VersionNumber,
                build = application.VersionBuild,
                language = application.Language.ToString(),
                shortCurveToleranceMm = Math.Round(application.ShortCurveTolerance * 304.8, 3),
                familyTemplatePath = application.FamilyTemplatePath,
                sharedParametersFile = application.SharedParametersFilename
            },
            commandSet = new
            {
                assembly = typeof(DiagnoseRevitEventHandler).Assembly.GetName().Name,
                version = typeof(DiagnoseRevitEventHandler).Assembly.GetName().Version?.ToString(),
                path = typeof(DiagnoseRevitEventHandler).Assembly.Location
            },
            active = activeDoc == null
                ? null
                : new { document = activeDoc.Title, view = app.ActiveUIDocument.ActiveView?.Name, isFamily = activeDoc.IsFamilyDocument },
            pinnedSession = session?.Describe(),
            documents,
            updaters,
            note = "Сторонние updater (не Autodesk) могут падать при изменениях и откатывать транзакции. Их можно отключить только из их надстройки."
        };
    }
}
