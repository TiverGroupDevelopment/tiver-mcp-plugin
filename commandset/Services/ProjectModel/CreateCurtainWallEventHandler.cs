using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Services.FamilyEditor;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.ProjectModel;

namespace RevitMCPCommandSet.Services.ProjectModel;

/// <summary>Стена или витраж по линии плана: тип, уровни, высота, смещение от линии.</summary>
public class CreateCurtainWallEventHandler : ProjectModelEventHandlerBase
{
    public override string GetName() => "Create curtain wall";

    protected override object Run(UIApplication app, JObject args)
    {
        var doc = ProjectResolver.Resolve(app, JsonArgs.OptionalString(args, "project"));
        var type = ProjectModelUtils.FindType(doc, JsonArgs.RequiredString(args, "type"), "wallType") as WallType
                   ?? throw new ArgumentException("type должен быть типом стены или витража");
        var start = ProjectModelUtils.PlanPointMm(JsonArgs.Required(args, "start"), "start");
        var end = ProjectModelUtils.PlanPointMm(JsonArgs.Required(args, "end"), "end");
        if (start.DistanceTo(end) < 1 / ProjectModelUtils.MmPerFoot) throw new ArgumentException("start и end совпадают");

        var offset = JsonArgs.OptionalDouble(args, "offset", 0) / ProjectModelUtils.MmPerFoot;
        if (Math.Abs(offset) > 1e-9)
        {
            var direction = (end - start).Normalize();
            var left = new XYZ(-direction.Y, direction.X, 0);
            start += left * offset;
            end += left * offset;
        }

        var baseLevel = ProjectModelUtils.FindLevel(doc, JsonArgs.RequiredString(args, "baseLevel"));
        var topLevelName = JsonArgs.OptionalString(args, "topLevel");
        var baseOffset = JsonArgs.OptionalDouble(args, "baseOffset", 0) / ProjectModelUtils.MmPerFoot;
        var topOffset = JsonArgs.OptionalDouble(args, "topOffset", 0) / ProjectModelUtils.MmPerFoot;
        var height = JsonArgs.OptionalDouble(args, "height", 3000) / ProjectModelUtils.MmPerFoot;
        var flip = JsonArgs.OptionalBool(args, "flip", false);
        var allowJoins = JsonArgs.OptionalBool(args, "allowJoins", true);

        var wall = InTransaction(doc, $"MCP: стена {type.Name}", failures =>
        {
            var line = Line.CreateBound(new XYZ(start.X, start.Y, baseLevel.Elevation), new XYZ(end.X, end.Y, baseLevel.Elevation));
            var created = Wall.Create(doc, line, type.Id, baseLevel.Id, height, baseOffset, flip, false);
            if (topLevelName != null)
            {
                var top = ProjectModelUtils.FindLevel(doc, topLevelName);
                created.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(top.Id);
                created.get_Parameter(BuiltInParameter.WALL_TOP_OFFSET).Set(topOffset);
            }

            if (!allowJoins)
            {
                WallUtils.DisallowWallJoinAtEnd(created, 0);
                WallUtils.DisallowWallJoinAtEnd(created, 1);
            }

            return created;
        });

        var uiDoc = app.ActiveUIDocument;
        if (uiDoc != null && uiDoc.Document.Equals(doc) && JsonArgs.OptionalBool(args, "select", true))
        {
            uiDoc.Selection.SetElementIds(new List<ElementId> { wall.Id });
        }

        var grid = wall.CurtainGrid;
        return new
        {
            id = wall.Id.GetValue(),
            project = doc.Title,
            type = type.Name,
            kind = type.Kind.ToString(),
            lengthMm = ProjectModelUtils.Mm(((LocationCurve)wall.Location).Curve.Length),
            heightMm = ProjectModelUtils.Mm(wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0),
            baseLevel = baseLevel.Name,
            topLevel = topLevelName,
            start = ProjectModelUtils.Mm(((LocationCurve)wall.Location).Curve.GetEndPoint(0)),
            end = ProjectModelUtils.Mm(((LocationCurve)wall.Location).Curve.GetEndPoint(1)),
            // У базовых стен Orientation смотрит наружу; у витражей Revit считает сторону иначе — сверяйте с основанием.
            orientation = new[] { Math.Round(wall.Orientation.X, 4), Math.Round(wall.Orientation.Y, 4) },
            flipped = wall.Flipped,
            curtainGrid = grid == null
                ? null
                : new { verticalLines = grid.GetVGridLineIds().Count, horizontalLines = grid.GetUGridLineIds().Count, mullions = grid.GetMullionIds().Count, panels = grid.GetPanelIds().Count }
        };
    }
}
