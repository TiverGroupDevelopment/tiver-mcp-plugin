using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class ModifyFamilyFormEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Modify family form";

    protected override object Run(UIApplication app, JObject args)
    {
        var elementId = JsonArgs.Required(args, "elementId").Value<long>();

        return Edit(app, args, "MCP: modify family form", doc =>
        {
            var form = FamilyFormUtils.GetForm(doc, elementId);
            var changed = new List<string>();

            switch (form)
            {
                case Extrusion extrusion:
                    ApplyExtrusion(extrusion, args, changed);
                    break;
                case Blend blend:
                    ApplyBlend(blend, args, changed);
                    break;
                case Revolution revolution:
                    ApplyRevolution(revolution, args, changed);
                    break;
            }

            RejectForeign(form, args, "start", "end", typeof(Extrusion));
            RejectForeign(form, args, "bottom", "top", typeof(Blend));
            RejectForeign(form, args, "startAngle", "endAngle", typeof(Revolution));

            if (JsonArgs.Has(args, "isSolid"))
            {
                var isSolid = JsonArgs.OptionalBool(args, "isSolid", true);
                if (isSolid != form.IsSolid)
                {
                    var parameter = form.get_Parameter(BuiltInParameter.ELEMENT_IS_CUTTING);
                    if (parameter == null || parameter.IsReadOnly)
                    {
                        throw new InvalidOperationException("Revit не позволяет сменить твёрдую форму на пустотную (или обратно) у этого элемента: удалите форму и создайте заново");
                    }

                    parameter.Set(isSolid ? 0 : 1);
                    changed.Add("isSolid");
                }
            }

            if (JsonArgs.Has(args, "visible"))
            {
                var parameter = form.get_Parameter(BuiltInParameter.IS_VISIBLE_PARAM)
                                ?? throw new InvalidOperationException("У формы нет параметра видимости");
                parameter.Set(JsonArgs.OptionalBool(args, "visible", true) ? 1 : 0);
                changed.Add("visible");
            }

            var material = JsonArgs.OptionalString(args, "material");
            if (material != null)
            {
                if (!form.IsSolid) throw new ArgumentException("Материал нельзя назначить пустотной форме");
                var parameter = form.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM)
                                ?? throw new InvalidOperationException("У формы нет параметра материала");
                parameter.Set(FamilyEditorIds.FindMaterial(doc, material).Id);
                changed.Add("material");
            }

            if (changed.Count == 0)
            {
                throw new ArgumentException("Не задано ни одного изменения. Профиль эскиза через API не меняется: удалите форму (delete_family_elements) и создайте заново.");
            }

            doc.Regenerate();

            object cuts = null;
            if (!form.IsSolid && changed.Contains("isSolid") && JsonArgs.OptionalBool(args, "cutIntersectingSolids", true))
            {
                cuts = FamilyFormUtils.CutIntersectingSolids(doc, form);
            }

            return new
            {
                changed,
                form = FamilyGeometryReader.DescribeDetailed(doc, doc.FamilyManager, form),
                cuts
            };
        });
    }

    private static void ApplyExtrusion(Extrusion extrusion, JObject args, List<string> changed)
    {
        var start = JsonArgs.Has(args, "start") ? JsonArgs.RequiredDouble(args, "start") / 304.8 : extrusion.StartOffset;
        var end = JsonArgs.Has(args, "end") ? JsonArgs.RequiredDouble(args, "end") / 304.8 : extrusion.EndOffset;
        if (!JsonArgs.Has(args, "start") && !JsonArgs.Has(args, "end")) return;
        if (Math.Abs(end - start) * 304.8 < 0.5) throw new ArgumentException("Глубина выдавливания (end - start) должна быть не меньше 0.5 мм");

        // Порядок присваивания выбран так, чтобы промежуточная глубина не обнулялась.
        if (end > extrusion.StartOffset)
        {
            extrusion.EndOffset = end;
            extrusion.StartOffset = start;
        }
        else
        {
            extrusion.StartOffset = start;
            extrusion.EndOffset = end;
        }

        changed.Add("extent");
    }

    private static void ApplyBlend(Blend blend, JObject args, List<string> changed)
    {
        if (!JsonArgs.Has(args, "bottom") && !JsonArgs.Has(args, "top")) return;
        var bottom = JsonArgs.Has(args, "bottom") ? JsonArgs.RequiredDouble(args, "bottom") / 304.8 : blend.BottomOffset;
        var top = JsonArgs.Has(args, "top") ? JsonArgs.RequiredDouble(args, "top") / 304.8 : blend.TopOffset;
        if (Math.Abs(top - bottom) * 304.8 < 0.5) throw new ArgumentException("Высота перехода (top - bottom) должна быть не меньше 0.5 мм");

        if (top > blend.BottomOffset)
        {
            blend.TopOffset = top;
            blend.BottomOffset = bottom;
        }
        else
        {
            blend.BottomOffset = bottom;
            blend.TopOffset = top;
        }

        changed.Add("extent");
    }

    private static void ApplyRevolution(Revolution revolution, JObject args, List<string> changed)
    {
        if (!JsonArgs.Has(args, "startAngle") && !JsonArgs.Has(args, "endAngle")) return;
        var start = JsonArgs.Has(args, "startAngle") ? JsonArgs.RequiredDouble(args, "startAngle") * Math.PI / 180.0 : revolution.StartAngle;
        var end = JsonArgs.Has(args, "endAngle") ? JsonArgs.RequiredDouble(args, "endAngle") * Math.PI / 180.0 : revolution.EndAngle;
        if (end - start <= 0 || end - start > 2 * Math.PI + 1e-9) throw new ArgumentException("Требуется 0 < endAngle - startAngle ≤ 360");

        revolution.StartAngle = start;
        revolution.EndAngle = end;
        changed.Add("angles");
    }

    private static void RejectForeign(GenericForm form, JObject args, string first, string second, Type owner)
    {
        if (owner.IsInstanceOfType(form)) return;
        if (JsonArgs.Has(args, first) || JsonArgs.Has(args, second))
        {
            throw new ArgumentException($"Параметры '{first}' / '{second}' применимы только к {owner.Name}, а элемент {form.Id.GetValue()} — {form.GetType().Name}");
        }
    }
}
