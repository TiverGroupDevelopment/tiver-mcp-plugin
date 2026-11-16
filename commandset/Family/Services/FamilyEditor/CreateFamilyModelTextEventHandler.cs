using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

/// <summary>
/// Объёмный текст модели в семействе (маркировка, надписи на гранях). Высота шрифта задаётся типом текста:
/// при heightMm/font нужный тип находится или создаётся копированием.
/// </summary>
public class CreateFamilyModelTextEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Create family model text";

    protected override object Run(UIApplication app, JObject args)
    {
        var text = JsonArgs.RequiredString(args, "text");
        var depth = JsonArgs.OptionalDouble(args, "depthMm", 1.0);
        if (depth <= 0) throw new ArgumentException("depthMm должна быть больше 0");
        var height = JsonArgs.Has(args, "heightMm") ? args["heightMm"].Value<double>() : (double?)null;
        var font = JsonArgs.OptionalString(args, "font");
        var align = ParseAlign(JsonArgs.OptionalString(args, "align") ?? "center");
        var materialName = JsonArgs.OptionalString(args, "material");

        return Edit(app, args, "MCP: текст модели", doc =>
        {
            if (!doc.IsFamilyDocument) throw new InvalidOperationException("Текст модели создаётся в документе семейства");
            var plane = FamilyWorkPlane.Create(doc, args["workPlane"], "XY");
            var position = args["position"] != null ? plane.ToWorld(args["position"]) : plane.ToWorld(0, 0);
            var type = ResolveType(doc, height, font);

            var modelText = doc.FamilyCreate.NewModelText(text, type, plane.SketchPlane, position, align, depth / 304.8)
                            ?? throw new InvalidOperationException("Revit не создал текст модели");

            if (materialName != null)
            {
                var material = FamilyEditorIds.FindMaterial(doc, materialName);
                var parameter = modelText.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM)
                                ?? throw new InvalidOperationException("У текста модели нет параметра материала");
                parameter.Set(material.Id);
            }

            doc.Regenerate();
            var box = modelText.get_BoundingBox(null);
            return new
            {
                id = modelText.Id.GetValue(),
                text,
                type = type.Name,
                heightMm = Math.Round((SizeParameter(type)?.AsDouble() ?? 0) * 304.8, 3),
                font = type.get_Parameter(BuiltInParameter.TEXT_FONT)?.AsString(),
                depthMm = depth,
                workPlane = plane.Describe(),
                boundingBoxMm = box == null ? null : new { min = FamilyDocInfo.Mm(box.Min), max = FamilyDocInfo.Mm(box.Max) }
            };
        });
    }

    private static ModelTextType ResolveType(Document doc, double? heightMm, string font)
    {
        var types = new FilteredElementCollector(doc).OfClass(typeof(ModelTextType)).Cast<ModelTextType>().ToList();
        if (types.Count == 0) throw new InvalidOperationException("В семействе нет типов текста модели");
        if (heightMm == null && font == null) return types[0];

        bool Matches(ModelTextType t) =>
            (heightMm == null || Math.Abs((SizeParameter(t)?.AsDouble() ?? 0) * 304.8 - heightMm.Value) < 1e-3)
            && (font == null || string.Equals(t.get_Parameter(BuiltInParameter.TEXT_FONT)?.AsString(), font, StringComparison.OrdinalIgnoreCase));

        var existing = types.FirstOrDefault(Matches);
        if (existing != null) return existing;

        var baseType = types[0];
        var name = $"MCP {(heightMm.HasValue ? heightMm.Value + " мм" : string.Empty)} {font}".Trim();
        var created = (ModelTextType)baseType.Duplicate(name);
        if (heightMm.HasValue)
        {
            if (heightMm.Value <= 0) throw new ArgumentException("heightMm должна быть больше 0");
            (SizeParameter(created) ?? throw new InvalidOperationException("У типа текста модели нет параметра размера")).Set(heightMm.Value / 304.8);
        }

        if (font != null)
        {
            (created.get_Parameter(BuiltInParameter.TEXT_FONT) ?? throw new InvalidOperationException("У типа текста модели нет параметра шрифта")).Set(font);
        }

        return created;
    }

    /// <summary>Размер текста модели: MODEL_TEXT_SIZE (у типа текста модели), TEXT_SIZE — на всякий случай.</summary>
    private static Parameter SizeParameter(Element type)
    {
        return type.get_Parameter(BuiltInParameter.MODEL_TEXT_SIZE) ?? type.get_Parameter(BuiltInParameter.TEXT_SIZE);
    }

    private static HorizontalAlign ParseAlign(string value)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "left": return HorizontalAlign.Left;
            case "right": return HorizontalAlign.Right;
            case "center": return HorizontalAlign.Center;
            default: throw new ArgumentException($"align: допустимо left, center, right; получено '{value}'");
        }
    }
}
