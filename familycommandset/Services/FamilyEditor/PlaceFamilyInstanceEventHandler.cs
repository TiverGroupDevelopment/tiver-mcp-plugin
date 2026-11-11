using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPFamilyCommandSet.Utils;
using RevitMCPFamilyCommandSet.Utils.FamilyEditor;

namespace RevitMCPFamilyCommandSet.Services.FamilyEditor;

public class PlaceFamilyInstanceEventHandler : FamilyEditorEventHandlerBase
{
    public override string GetName() => "Place family instance";

    protected override object Run(UIApplication app, JObject args)
    {
        var nestedFamily = JsonArgs.RequiredString(args, "nestedFamily");
        var typeName = JsonArgs.OptionalString(args, "type");
        var location = JsonArgs.Has(args, "location") ? JsonArgs.PointMm(args["location"], "location") : XYZ.Zero;
        var rotation = JsonArgs.OptionalDouble(args, "rotation", 0) * Math.PI / 180.0;
        var levelName = JsonArgs.OptionalString(args, "level");

        return Edit(app, args, "MCP: place nested family instance", doc =>
        {
            var family = new FilteredElementCollector(doc)
                             .OfClass(typeof(Family))
                             .Cast<Family>()
                             .FirstOrDefault(f => string.Equals(f.Name, nestedFamily, StringComparison.OrdinalIgnoreCase))
                         ?? throw new ArgumentException($"Семейство '{nestedFamily}' не загружено в это семейство. Загрузить: load_nested_family.");

            var symbols = family.GetFamilySymbolIds().Select(id => doc.GetElement(id)).OfType<FamilySymbol>().ToList();
            if (symbols.Count == 0) throw new InvalidOperationException($"У семейства '{family.Name}' нет типоразмеров");

            var symbol = typeName == null
                ? symbols[0]
                : symbols.FirstOrDefault(s => string.Equals(s.Name, typeName, StringComparison.OrdinalIgnoreCase))
                  ?? throw new ArgumentException(
                      $"Типоразмер '{typeName}' не найден в '{family.Name}'. Доступные: {string.Join(", ", symbols.Select(s => s.Name))}");

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            FamilyInstance instance;
            if (levelName != null)
            {
                var level = new FilteredElementCollector(doc)
                                .OfClass(typeof(Level))
                                .Cast<Level>()
                                .FirstOrDefault(l => string.Equals(l.Name, levelName, StringComparison.OrdinalIgnoreCase))
                            ?? throw new ArgumentException($"Уровень '{levelName}' не найден в семействе");
                instance = doc.FamilyCreate.NewFamilyInstance(location, symbol, level, StructuralType.NonStructural);
            }
            else
            {
                instance = doc.FamilyCreate.NewFamilyInstance(location, symbol, StructuralType.NonStructural);
            }

            if (instance == null) throw new InvalidOperationException("Revit не разместил экземпляр вложенного семейства");

            if (Math.Abs(rotation) > 1e-9)
            {
                ElementTransformUtils.RotateElement(doc, instance.Id, Line.CreateUnbound(location, XYZ.BasisZ), rotation);
            }

            doc.Regenerate();
            return new
            {
                id = instance.Id.GetValue(),
                family = family.Name,
                type = symbol.Name,
                locationMm = FamilyDocInfo.Mm(location),
                associableParameters = FamilyFormUtils.AssociableParameters(doc.FamilyManager, instance)
            };
        });
    }
}
