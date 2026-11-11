using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

public static class FamilyFormUtils
{
    public static object Finish(Document doc, GenericForm form, JObject args, FamilyWorkPlane plane)
    {
        var material = JsonArgs.OptionalString(args, "material");
        if (material != null)
        {
            if (!form.IsSolid) throw new ArgumentException("Материал нельзя назначить пустотной форме");
            var parameter = form.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM)
                            ?? throw new InvalidOperationException("У формы нет параметра материала");
            parameter.Set(FamilyEditorIds.FindMaterial(doc, material).Id);
        }

        doc.Regenerate();

        object cuts = null;
        if (!form.IsSolid && JsonArgs.OptionalBool(args, "cutIntersectingSolids", true))
        {
            cuts = CutIntersectingSolids(doc, form);
        }

        return new
        {
            form = Describe(doc, doc.FamilyManager, form),
            workPlane = plane.Describe(),
            cuts
        };
    }

    public static object CutIntersectingSolids(Document doc, GenericForm voidForm)
    {
        var applied = new List<long>();
        var failed = new List<object>();
        var voidBox = voidForm.get_BoundingBox(null);

        var solids = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .ToElements()
            .OfType<GenericForm>()
            .Where(f => f.IsSolid && Overlaps(voidBox, f.get_BoundingBox(null)));

        foreach (var solid in solids)
        {
            try
            {
                if (SolidSolidCutUtils.CutExistsBetweenElements(solid, voidForm, out _)) continue;
                if (!SolidSolidCutUtils.CanElementCutElement(voidForm, solid, out var reason))
                {
                    failed.Add(new { id = solid.Id.GetValue(), reason = reason.ToString() });
                    continue;
                }

                SolidSolidCutUtils.AddCutBetweenSolids(doc, solid, voidForm);
                applied.Add(solid.Id.GetValue());
            }
            catch (Exception ex)
            {
                failed.Add(new { id = solid.Id.GetValue(), reason = ex.Message });
            }
        }

        return new { applied, failed };
    }

    public static object Describe(Document doc, FamilyManager manager, GenericForm form)
    {
        var box = form.get_BoundingBox(null);

        return new
        {
            id = form.Id.GetValue(),
            kind = form.GetType().Name,
            isSolid = form.IsSolid,
            name = form.Name,
            boundingBoxMm = box == null
                ? null
                : new
                {
                    min = new[] { Mm(box.Min.X), Mm(box.Min.Y), Mm(box.Min.Z) },
                    max = new[] { Mm(box.Max.X), Mm(box.Max.Y), Mm(box.Max.Z) }
                },
            associableParameters = AssociableParameters(manager, form)
        };
    }

    /// <summary>Параметры элемента (формы, вложенного экземпляра, коннектора), которые можно связать с параметрами семейства.</summary>
    public static List<object> AssociableParameters(FamilyManager manager, Element element)
    {
        var parameters = new List<object>();
        foreach (Parameter parameter in element.Parameters)
        {
            bool associable;
            try
            {
                associable = manager.CanElementParameterBeAssociated(parameter);
            }
            catch (Exception)
            {
                associable = false;
            }

            if (!associable) continue;

            var builtIn = (parameter.Definition as InternalDefinition)?.BuiltInParameter ?? BuiltInParameter.INVALID;
            parameters.Add(new
            {
                name = parameter.Definition.Name,
                builtIn = builtIn == BuiltInParameter.INVALID ? null : builtIn.ToString(),
                value = parameter.AsValueString() ?? parameter.AsString(),
                associatedFamilyParameter = manager.GetAssociatedFamilyParameter(parameter)?.Definition.Name
            });
        }

        return parameters;
    }

    /// <summary>Описание любого элемента семейства: форма — подробно, остальное — тип, имя и связываемые параметры.</summary>
    public static object DescribeElement(Document doc, FamilyManager manager, Element element)
    {
        if (element is GenericForm form) return Describe(doc, manager, form);
        return new
        {
            id = element.Id.GetValue(),
            kind = element.GetType().Name,
            name = element.Name,
            associableParameters = AssociableParameters(manager, element)
        };
    }

    public static GenericForm GetForm(Document doc, long id)
    {
        return doc.GetElement(FamilyEditorIds.FromNumber(id)) as GenericForm
               ?? throw new ArgumentException($"Элемент {id} не найден или не является формой (Extrusion/Blend/Revolution/...)");
    }

    private static double Mm(double feet)
    {
        return Math.Round(feet * 304.8, 3);
    }

    private static bool Overlaps(BoundingBoxXYZ a, BoundingBoxXYZ b)
    {
        if (a == null || b == null) return true;
        return a.Min.X <= b.Max.X && a.Max.X >= b.Min.X
               && a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y
               && a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
    }
}
