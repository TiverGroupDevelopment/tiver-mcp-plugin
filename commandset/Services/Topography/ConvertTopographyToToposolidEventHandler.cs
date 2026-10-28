using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;
using RevitMCPCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.Topography;

namespace RevitMCPCommandSet.Services.Topography;

public class ConvertTopographyToToposolidEventHandler : TopographyEventHandlerBase
{
    public override string GetName() => "Convert topography to toposolid";

    protected override object Run(Document doc, JObject args)
    {
#if REVIT2024_OR_GREATER
        var surface = TopoUtils.Resolve(doc, args) as TopographySurface
                      ?? throw new ArgumentException("Конвертировать можно только топоповерхность (TopographySurface)");
        if (surface.IsSiteSubRegion)
        {
            throw new ArgumentException("Укажите основную топоповерхность: подобласти конвертируются вместе с ней");
        }

        var deleteSource = JsonArgs.OptionalBool(args, "deleteSource", false);

        return Edit(doc, "MCP: топоповерхность → топотело", () =>
        {
            var minZ = surface.GetPoints().Min(p => p.Z);
            var level = TopoUtils.ResolveLevel(doc, JsonArgs.OptionalString(args, "levelName"), minZ);
            var type = TopoUtils.ResolveToposolidType(doc, JsonArgs.OptionalString(args, "typeName"));
            var sourceId = surface.Id.GetValue();

            var toposolid = Toposolid.CreateFromTopographySurface(doc, surface.Id, type.Id, level.Id);
            var subdivisionIds = toposolid.GetSubDivisionIds().Select(id => id.GetValue()).ToList();

            if (deleteSource)
            {
                doc.Delete(surface.Id);
            }

            return new
            {
                id = toposolid.Id.GetValue(),
                sourceId,
                sourceDeleted = deleteSource,
                typeName = type.Name,
                level = level.Name,
                subdivisionIds
            };
        });
#else
        throw new NotSupportedException("Топотела (Toposolid) доступны с Revit 2024");
#endif
    }
}
