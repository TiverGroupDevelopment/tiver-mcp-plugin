using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;
using RevitMCPCommandSet.Utils.FamilyEditor;
using RevitMCPCommandSet.Utils.Topography;

// API TopographySurface устарел в 2024, но нужен для Revit 2020–2023 и существующих моделей
#pragma warning disable CS0618

namespace RevitMCPCommandSet.Services.Topography;

public class CreateTopographySubregionEventHandler : TopographyEventHandlerBase
{
    public override string GetName() => "Create topography subregion";

    protected override object Run(Document doc, JObject args)
    {
        var host = TopoUtils.Resolve(doc, args);
        if (TopoUtils.IsChild(host))
        {
            throw new ArgumentException($"Элемент {host.Id.GetValue()} сам является подобластью: укажите основную топоповерхность");
        }

        var boundary = JsonArgs.Required(args, "boundary");

        switch (host)
        {
            case TopographySurface surface:
                return Edit(doc, "MCP: создать подобласть топоповерхности", () => CreateSiteSubRegion(doc, surface, boundary, args));
#if REVIT2024_OR_GREATER
            case Toposolid solid:
                return Edit(doc, "MCP: создать подразделение топотела", () => CreateSubdivision(doc, solid, boundary, args));
#endif
            default:
                throw new ArgumentException($"Элемент {host.Id.GetValue()} не является топоповерхностью");
        }
    }

    private static object CreateSiteSubRegion(Document doc, TopographySurface surface, JToken boundary, JObject args)
    {
        if (JsonArgs.Has(args, "typeName"))
        {
            throw new ArgumentException("typeName применим только к топотелам; для подобласти топоповерхности используйте material");
        }

        var loops = TopoUtils.BuildLoops(doc, boundary, 0);
        if (!SiteSubRegion.IsValidBoundary(loops))
        {
            throw new ArgumentException("Контур подобласти некорректен: контуры должны быть замкнуты и не пересекаться");
        }

        var region = SiteSubRegion.Create(doc, loops, surface.Id);
        var materialId = TopoUtils.ResolveMaterial(doc, JsonArgs.OptionalString(args, "material"));
        if (materialId != null)
        {
            region.TopographySurface.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM).Set(materialId);
        }

        return new
        {
            id = region.TopographySurface.Id.GetValue(),
            kind = "siteSubRegion",
            hostId = surface.Id.GetValue(),
            material = TopoUtils.MaterialName(doc, region.TopographySurface)
        };
    }

#if REVIT2024_OR_GREATER
    private static object CreateSubdivision(Document doc, Toposolid solid, JToken boundary, JObject args)
    {
        if (JsonArgs.Has(args, "material"))
        {
            throw new ArgumentException("Материал подразделения топотела задаётся его типом: передайте typeName");
        }

        var level = doc.GetElement(solid.LevelId) as Level;
        var loops = TopoUtils.BuildLoops(doc, boundary, level?.Elevation ?? 0);
        var subdivision = solid.CreateSubDivision(doc, loops);

        var typeName = JsonArgs.OptionalString(args, "typeName");
        if (typeName != null)
        {
            subdivision.ChangeTypeId(TopoUtils.ResolveToposolidType(doc, typeName).Id);
        }

        return new
        {
            id = subdivision.Id.GetValue(),
            kind = "toposolidSubdivision",
            hostId = solid.Id.GetValue(),
            typeName = doc.GetElement(subdivision.GetTypeId())?.Name,
            material = TopoUtils.MaterialName(doc, subdivision)
        };
    }
#endif
}
