using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;
using RevitMCPCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.Topography;

// API TopographySurface устарел в 2024, но нужен для Revit 2020–2023 и существующих моделей
#pragma warning disable CS0618

namespace RevitMCPCommandSet.Services.Topography;

public class CreateTopographyEventHandler : TopographyEventHandlerBase
{
    public override string GetName() => "Create topography";

    protected override object Run(Document doc, JObject args)
    {
        var points = TopoUtils.ParsePoints3(args, "points");
        if (points.Count < 3) throw new ArgumentException("Нужно минимум 3 точки");

        var kind = (JsonArgs.OptionalString(args, "kind") ?? "auto").ToLowerInvariant();
#if REVIT2024_OR_GREATER
        if (kind == "auto") kind = "toposolid";
#else
        if (kind == "auto") kind = "topographysurface";
#endif

        switch (kind)
        {
            case "topographysurface":
                return Edit(doc, "MCP: создать топоповерхность", () => CreateSurface(doc, args, points));
            case "toposolid":
#if REVIT2024_OR_GREATER
                return Edit(doc, "MCP: создать топотело", () => CreateToposolid(doc, args, points));
#else
                throw new NotSupportedException("Топотела (Toposolid) доступны с Revit 2024. Используйте kind: topographySurface");
#endif
            default:
                throw new ArgumentException($"Неизвестный kind '{kind}'. Допустимо: auto, topographySurface, toposolid");
        }
    }

    private static object CreateSurface(Document doc, JObject args, List<XYZ> points)
    {
        if (!TopographySurface.ArePointsDistinct(points))
        {
            throw new ArgumentException("Точки должны быть различны в плане (совпадают X и Y у двух или более точек)");
        }

        var surface = TopographySurface.Create(doc, points);
        var materialId = TopoUtils.ResolveMaterial(doc, JsonArgs.OptionalString(args, "material"));
        if (materialId != null)
        {
            surface.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM).Set(materialId);
        }

        return new
        {
            id = surface.Id.GetValue(),
            kind = "topographySurface",
            pointCount = points.Count,
            material = TopoUtils.MaterialName(doc, surface)
        };
    }

#if REVIT2024_OR_GREATER
    private static object CreateToposolid(Document doc, JObject args, List<XYZ> points)
    {
        if (JsonArgs.Has(args, "material"))
        {
            throw new ArgumentException("Материал топотела задаётся его типом: передайте typeName вместо material");
        }

        var level = TopoUtils.ResolveLevel(doc, JsonArgs.OptionalString(args, "levelName"), points.Min(p => p.Z));
        var type = TopoUtils.ResolveToposolidType(doc, JsonArgs.OptionalString(args, "typeName"));

        var toposolid = JsonArgs.Has(args, "boundary")
            ? Toposolid.Create(doc, TopoUtils.BuildLoops(doc, args["boundary"], level.Elevation), points, type.Id, level.Id)
            : Toposolid.Create(doc, points, type.Id, level.Id);

        return new
        {
            id = toposolid.Id.GetValue(),
            kind = "toposolid",
            typeName = type.Name,
            level = level.Name,
            pointCount = points.Count,
            material = TopoUtils.MaterialName(doc, toposolid)
        };
    }
#endif
}
