using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class CreateFamilyArrayEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family array";

    protected override object Run(UIApplication app, JObject args)
    {
        var ids = JsonArgs.Ids(args, "elementIds");
        var count = (int)JsonArgs.RequiredDouble(args, "count");
        if (count < 2) throw new ArgumentException("count должен быть не меньше 2");
        var hasLinear = JsonArgs.Has(args, "translation");
        var hasRadial = JsonArgs.Has(args, "radial");
        if (hasLinear == hasRadial) throw new ArgumentException("Задайте либо translation (линейный массив), либо radial (круговой)");

        var anchorText = (JsonArgs.OptionalString(args, "anchor") ?? "second").ToLowerInvariant();
        ArrayAnchorMember anchor;
        switch (anchorText)
        {
            case "second":
                anchor = ArrayAnchorMember.Second;
                break;
            case "last":
                anchor = ArrayAnchorMember.Last;
                break;
            default:
                throw new ArgumentException($"Неизвестный anchor '{anchorText}'. Допустимо: second (шаг до второго элемента), last (до последнего)");
        }

        return Edit(app, args, "MCP: create family array", doc =>
        {
            var elementIds = ids.Select(FamilyEditorIds.FromNumber).ToList();
            foreach (var id in elementIds)
            {
                if (doc.GetElement(id) == null) throw new ArgumentException($"Элемент {id.GetValue()} не найден");
            }

            var viewName = JsonArgs.OptionalString(args, "view");
            var view = viewName != null
                ? FamilyDocInfo.FindView(doc, viewName)
                : FamilyDocInfo.Views(doc).FirstOrDefault(v => v.ViewType == ViewType.FloorPlan)
                  ?? throw new InvalidOperationException("В семействе нет плана для построения массива: укажите view");

            BaseArray array;
            if (hasLinear)
            {

                array = LinearArray.Create(doc, view, elementIds, count, JsonArgs.PointMm(args["translation"], "translation"), anchor);
            }
            else
            {
                var radial = JsonArgs.RequiredObject(args, "radial");
                var origin = JsonArgs.Has(radial, "origin") ? JsonArgs.PointMm(radial["origin"], "radial.origin") : XYZ.Zero;
                var axis = JsonArgs.Has(radial, "axis") ? JsonArgs.Vector(radial["axis"], "radial.axis") : XYZ.BasisZ;
                if (axis.GetLength() < 1e-9) throw new ArgumentException("radial.axis не может быть нулевым вектором");
                var angle = JsonArgs.RequiredDouble(radial, "angle") * Math.PI / 180.0;

                array = RadialArray.Create(doc, view, elementIds, count, Line.CreateUnbound(origin, axis.Normalize()), angle, anchor);
            }

            if (array == null) throw new InvalidOperationException("Revit не создал массив");

            var countParameter = JsonArgs.OptionalString(args, "countParameter");
            if (countParameter != null)
            {
                try
                {
                    array.Label = FamilyParameterSpecs.Find(doc.FamilyManager, countParameter);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Не удалось связать число элементов массива с параметром '{countParameter}': {ex.Message}. Параметр должен быть типа Integer.", ex);
                }
            }

            doc.Regenerate();
            return new
            {
                id = array.Id.GetValue(),
                kind = array.GetType().Name,
                count = array.NumMembers,
                countParameter,
                view = view.Name
            };
        });
    }
}
