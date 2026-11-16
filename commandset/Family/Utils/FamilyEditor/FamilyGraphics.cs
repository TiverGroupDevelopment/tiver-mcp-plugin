using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

/// <summary>Подкатегория и видимость элементов семейства (формы, модельные и символьные линии).</summary>
public static class FamilyGraphics
{
    public static void SetSubcategory(Document doc, Element element, string subcategoryName)
    {
        var subcategory = FamilyProperties.FindSubcategory(doc, subcategoryName);
        switch (element)
        {
            case GenericForm form:
                form.Subcategory = subcategory;
                break;
            case ModelCurve modelCurve:
                modelCurve.Subcategory = LineStyle(subcategory, GraphicsStyleType.Projection);
                break;
            case SymbolicCurve symbolicCurve:
                symbolicCurve.Subcategory = LineStyle(subcategory, GraphicsStyleType.Projection);
                break;
            default:
                throw new ArgumentException($"Элементу {element.Id.GetValue()} ({element.GetType().Name}) нельзя назначить подкатегорию");
        }
    }

    public static void SetVisibility(Element element, JObject spec)
    {
        FamilyElementVisibility visibility;
        switch (element)
        {
            case GenericForm form:
                visibility = form.GetVisibility();
                Apply(visibility, spec);
                form.SetVisibility(visibility);
                break;
            case ModelCurve modelCurve:
                visibility = modelCurve.GetVisibility();
                Apply(visibility, spec);
                modelCurve.SetVisibility(visibility);
                break;
            case SymbolicCurve symbolicCurve:
                visibility = symbolicCurve.GetVisibility();
                Apply(visibility, spec);
                symbolicCurve.SetVisibility(visibility);
                break;
            default:
                throw new ArgumentException($"У элемента {element.Id.GetValue()} ({element.GetType().Name}) нет настроек видимости семейства");
        }
    }

    public static object Describe(Element element)
    {
        FamilyElementVisibility visibility = null;
        string subcategory = null;
        try
        {
            switch (element)
            {
                case GenericForm form:
                    visibility = form.GetVisibility();
                    subcategory = form.Subcategory?.Name;
                    break;
                case ModelCurve modelCurve:
                    visibility = modelCurve.GetVisibility();
                    subcategory = modelCurve.Subcategory?.GraphicsStyleCategory?.Name;
                    break;
                case SymbolicCurve symbolicCurve:
                    visibility = symbolicCurve.GetVisibility();
                    subcategory = symbolicCurve.Subcategory?.GraphicsStyleCategory?.Name;
                    break;
            }
        }
        catch (Exception)
        {
        }

        return new
        {
            id = element.Id.GetValue(),
            kind = element.GetType().Name,
            subcategory,
            visibility = visibility == null
                ? null
                : new
                {
                    coarse = visibility.IsShownInCoarse,
                    medium = visibility.IsShownInMedium,
                    fine = visibility.IsShownInFine,
                    planRcp = visibility.IsShownInPlanRCPCut,
                    frontBack = visibility.IsShownInFrontBack,
                    leftRight = visibility.IsShownInLeftRight,
                    topBottom = visibility.IsShownInTopBottom,
                    onlyWhenCut = visibility.IsShownOnlyWhenCut
                }
        };
    }

    private static void Apply(FamilyElementVisibility visibility, JObject spec)
    {
        if (JsonArgs.Has(spec, "coarse")) visibility.IsShownInCoarse = spec["coarse"].Value<bool>();
        if (JsonArgs.Has(spec, "medium")) visibility.IsShownInMedium = spec["medium"].Value<bool>();
        if (JsonArgs.Has(spec, "fine")) visibility.IsShownInFine = spec["fine"].Value<bool>();
        if (JsonArgs.Has(spec, "planRcp")) visibility.IsShownInPlanRCPCut = spec["planRcp"].Value<bool>();
        if (JsonArgs.Has(spec, "frontBack")) visibility.IsShownInFrontBack = spec["frontBack"].Value<bool>();
        if (JsonArgs.Has(spec, "leftRight")) visibility.IsShownInLeftRight = spec["leftRight"].Value<bool>();
        if (JsonArgs.Has(spec, "topBottom")) visibility.IsShownInTopBottom = spec["topBottom"].Value<bool>();
        if (JsonArgs.Has(spec, "onlyWhenCut")) visibility.IsShownOnlyWhenCut = spec["onlyWhenCut"].Value<bool>();
    }

    private static GraphicsStyle LineStyle(Category subcategory, GraphicsStyleType type)
    {
        return subcategory.GetGraphicsStyle(type)
               ?? throw new InvalidOperationException($"У подкатегории '{subcategory.Name}' нет стиля линий");
    }
}
