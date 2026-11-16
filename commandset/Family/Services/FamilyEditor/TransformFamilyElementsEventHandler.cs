using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class TransformFamilyElementsEventHandler : FamilyEditorEventHandlerBase
{
    private static readonly string[] Operations = { "move", "copy", "rotate", "mirror" };

    public override string GetName() => "Transform family elements";

    protected override object Run(UIApplication app, JObject args)
    {
        var ids = JsonArgs.Ids(args, "elementIds");
        var given = Operations.Where(o => JsonArgs.Has(args, o)).ToList();
        if (given.Count != 1)
        {
            throw new ArgumentException($"Задайте ровно одну операцию из: {string.Join(", ", Operations)}");
        }

        var operation = given[0];

        return Edit(app, args, $"MCP: {operation} family elements", doc =>
        {
            var elementIds = ids.Select(FamilyEditorIds.FromNumber).ToList();
            foreach (var id in elementIds)
            {
                if (doc.GetElement(id) == null) throw new ArgumentException($"Элемент {id.GetValue()} не найден");
            }

            var createdIds = new List<ElementId>();
            switch (operation)
            {
                case "move":
                    ElementTransformUtils.MoveElements(doc, elementIds, JsonArgs.PointMm(args["move"], "move"));
                    break;

                case "copy":
                    createdIds.AddRange(ElementTransformUtils.CopyElements(doc, elementIds, JsonArgs.PointMm(args["copy"], "copy")));
                    break;

                case "rotate":
                    var rotate = JsonArgs.RequiredObject(args, "rotate");
                    var axisOrigin = JsonArgs.Has(rotate, "origin") ? JsonArgs.PointMm(rotate["origin"], "rotate.origin") : XYZ.Zero;
                    var axisDirection = JsonArgs.Has(rotate, "axis") ? JsonArgs.Vector(rotate["axis"], "rotate.axis") : XYZ.BasisZ;
                    if (axisDirection.GetLength() < 1e-9) throw new ArgumentException("rotate.axis не может быть нулевым вектором");
                    var angle = JsonArgs.RequiredDouble(rotate, "angle") * Math.PI / 180.0;
                    ElementTransformUtils.RotateElements(doc, elementIds, Line.CreateUnbound(axisOrigin, axisDirection.Normalize()), angle);
                    break;

                case "mirror":
                    var mirror = JsonArgs.RequiredObject(args, "mirror");
                    var plane = MirrorPlane(doc, mirror);
                    if (!ElementTransformUtils.CanMirrorElements(doc, elementIds))
                    {
                        throw new InvalidOperationException("Revit не может отразить указанные элементы");
                    }

                    var keepOriginal = JsonArgs.OptionalBool(mirror, "copy", true);
                    var mirrored = ElementTransformUtils.MirrorElements(doc, elementIds, plane, keepOriginal);
                    if (keepOriginal) createdIds.AddRange(mirrored);
                    break;
            }

            doc.Regenerate();

            var manager = doc.FamilyManager;
            var affected = (createdIds.Count > 0 ? createdIds : elementIds)
                .Select(id => doc.GetElement(id))
                .Where(e => e != null)
                .Select(e => e is GenericForm form
                    ? FamilyFormUtils.Describe(doc, manager, form)
                    : new { id = e.Id.GetValue(), kind = e.GetType().Name, name = e.Name })
                .ToList();

            return new
            {
                operation,
                createdIds = createdIds.Select(id => id.GetValue()).ToList(),
                elements = affected
            };
        });
    }

    private static Plane MirrorPlane(Document doc, JObject mirror)
    {
        var referencePlane = JsonArgs.OptionalString(mirror, "referencePlane");
        if (referencePlane != null)
        {
            return FamilyDocInfo.FindReferencePlane(doc, referencePlane).GetPlane();
        }

        if (!JsonArgs.Has(mirror, "normal"))
        {
            throw new ArgumentException("mirror: задайте referencePlane (имя) либо normal и origin");
        }

        var normal = JsonArgs.Vector(mirror["normal"], "mirror.normal");
        if (normal.GetLength() < 1e-9) throw new ArgumentException("mirror.normal не может быть нулевым вектором");
        var origin = JsonArgs.Has(mirror, "origin") ? JsonArgs.PointMm(mirror["origin"], "mirror.origin") : XYZ.Zero;
        return Plane.CreateByNormalAndOrigin(normal.Normalize(), origin);
    }
}
