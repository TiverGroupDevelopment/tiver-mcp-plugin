using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class AlignFamilyElementsEventHandler : FamilyEditorEventHandlerBase
{
    // NewAlignment только фиксирует уже совпадающие плоскости, сам ничего не двигает.
    private const double CoincidenceToleranceFeet = 1e-5;

    public override string GetName() => "Align family elements";

    protected override object Run(UIApplication app, JObject args)
    {
        var specs = JsonArgs.RequiredArray(args, "alignments").OfType<JObject>().ToList();
        if (specs.Count == 0) throw new ArgumentException("alignments пуст");

        return Edit(app, args, "MCP: align family elements", doc =>
        {
            var created = new List<object>();
            foreach (var spec in specs)
            {
                var anchor = FamilyGeometryReader.Resolve(doc, JsonArgs.Required(spec, "reference"));
                var target = FamilyGeometryReader.Resolve(doc, JsonArgs.Required(spec, "target"));

                if (!FamilyGeometryReader.AreParallel(anchor.Normal, target.Normal))
                {
                    throw new ArgumentException($"'{target.Description}' не параллельна '{anchor.Description}': выровнять можно только параллельные плоскости");
                }

                var gap = Math.Abs((target.Point - anchor.Point).DotProduct(anchor.Normal));
                if (gap > CoincidenceToleranceFeet)
                {
                    throw new InvalidOperationException(
                        $"'{target.Description}' отстоит от '{anchor.Description}' на {FamilyDocInfo.Mm(gap)} мм. Выравнивание лишь закрепляет совпадающие плоскости: сначала постройте или переместите геометрию точно на опорную плоскость.");
                }

                var view = FamilyDocInfo.PickViewForNormal(doc, anchor.Normal, JsonArgs.OptionalString(spec, "view"));
                var alignment = doc.FamilyCreate.NewAlignment(view, anchor.Reference, target.Reference)
                                ?? throw new InvalidOperationException($"Revit не создал выравнивание '{target.Description}' → '{anchor.Description}'");

                created.Add(new
                {
                    id = alignment.Id.GetValue(),
                    reference = anchor.Description,
                    target = target.Description,
                    view = view.Name
                });
            }

            doc.Regenerate();
            return created;
        });
    }
}
