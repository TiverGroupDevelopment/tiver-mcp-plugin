using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

/// <summary>
/// Проверки коннекторов многопортовых деталей (коллекторы, тройники, крестовины): у них проходная магистраль
/// и отводы, поэтому проверки проходной детали «все коннекторы связаны» и «Y всех коннекторов вверх» не подходят.
/// Подключаются в check_family_rules ключами mainRun, connectorOrientationByDirection, partType.
///
/// Класс самодостаточный (только Revit API), стиль C# 5: тот же файл подключается в send_code_to_revit (classes).
/// Только чтение.
/// </summary>
public static class MultiPortChecks
{
    public sealed class Outcome
    {
        public string Status;
        public string Summary;
        public List<string> Details = new List<string>();

        public Outcome(string status, string summary)
        {
            Status = status;
            Summary = summary;
        }
    }

    public static List<ConnectorElement> Connectors(Document doc)
    {
        return new FilteredElementCollector(doc).OfClass(typeof(ConnectorElement)).Cast<ConnectorElement>().ToList();
    }

    /// <summary>
    /// Магистраль: ровно два коннектора смотрят вдоль axis (в разные стороны), стоят на оси через начало координат,
    /// связаны друг с другом, один из них главный. Остальные коннекторы — отводы, связь не обязательна.
    /// </summary>
    public static Outcome MainRun(Document doc, XYZ axis, double toleranceFeet)
    {
        axis = axis.Normalize();
        var all = Connectors(doc);
        if (all.Count == 0) return new Outcome("fail", "Коннекторов нет");
        var main = all.Where(c => Math.Abs(c.CoordinateSystem.BasisZ.Normalize().DotProduct(axis)) > 0.99).ToList();
        var branches = all.Count - main.Count;
        var details = new List<string>();
        foreach (var c in all) details.Add(Describe(c));

        Outcome result;
        if (main.Count != 2)
        {
            result = new Outcome("fail", "Магистраль: вдоль оси [" + Vec(axis) + "] должно смотреть ровно 2 коннектора, найдено " + main.Count);
        }
        else
        {
            var problems = new List<string>();
            var a = main[0];
            var b = main[1];
            if (a.CoordinateSystem.BasisZ.DotProduct(b.CoordinateSystem.BasisZ) > -0.99) problems.Add("коннекторы магистрали смотрят в одну сторону");
            foreach (var c in main)
            {
                var o = c.Origin;
                var offAxis = (o - axis.Multiply(o.DotProduct(axis))).GetLength();
                if (offAxis > toleranceFeet) problems.Add("коннектор " + c.Id + " в стороне от оси магистрали на " + Math.Round(offAxis * 304.8, 2) + " мм");
            }

            var linkedA = a.GetLinkedConnectorElement();
            var linkedB = b.GetLinkedConnectorElement();
            if (linkedA == null || linkedA.Id != b.Id || linkedB == null || linkedB.Id != a.Id) problems.Add("коннекторы магистрали " + a.Id + " и " + b.Id + " не связаны друг с другом");
            if (!a.IsPrimary && !b.IsPrimary) problems.Add("главный коннектор не на магистрали");
            result = problems.Count == 0
                ? new Outcome("pass", "Магистраль: 2 коннектора на оси, связаны, главный на магистрали; отводов " + branches)
                : new Outcome("fail", "Магистраль собрана неверно: " + string.Join("; ", problems));
        }

        result.Details.AddRange(details);
        return result;
    }

    /// <summary>
    /// Ориентация по направлению коннектора: у горизонтального коннектора ось Y — вверх (up),
    /// у коннектора, который смотрит вверх или вниз, ось Y — вдоль verticalY (обычно вдоль магистрали, +X).
    /// </summary>
    public static Outcome OrientationByDirection(Document doc, XYZ up, XYZ verticalY)
    {
        up = up.Normalize();
        verticalY = verticalY.Normalize();
        var all = Connectors(doc);
        if (all.Count == 0) return new Outcome("pass", "Коннекторов нет");
        var wrong = new List<string>();
        int vertical = 0;
        foreach (var c in all)
        {
            var z = c.CoordinateSystem.BasisZ.Normalize();
            var y = c.CoordinateSystem.BasisY.Normalize();
            var isVertical = Math.Abs(z.DotProduct(up)) > 0.99;
            if (isVertical) vertical++;
            var expected = isVertical ? verticalY : up;
            if (y.DotProduct(expected) < 0.99)
            {
                wrong.Add("коннектор " + c.Id + (isVertical ? " (вертикальный)" : " (горизонтальный)") + ": ось Y = [" + Vec(y) + "], нужно [" + Vec(expected) + "]");
            }
        }

        var outcome = wrong.Count == 0
            ? new Outcome("pass", "Ось Y горизонтальных коннекторов вверх, вертикальных — вдоль [" + Vec(verticalY) + "] (" + (all.Count - vertical) + " гориз., " + vertical + " верт.)")
            : new Outcome("fail", "Ориентация коннекторов не по правилу: при вставке деталь повернётся вокруг трубы");
        outcome.Details.AddRange(wrong);
        return outcome;
    }

    /// <summary>Тип детали семейства (FAMILY_CONTENT_PART_TYPE) — один из разрешённых.</summary>
    public static Outcome PartTypeIs(Document doc, IList<string> allowed)
    {
        var p = doc.OwnerFamily == null ? null : doc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_CONTENT_PART_TYPE);
        var name = p == null ? null : Enum.GetName(typeof(PartType), p.AsInteger());
        if (name == null) return new Outcome("fail", "У семейства нет типа детали (категория без MEP-типа детали?)");
        if (allowed == null || allowed.Count == 0 || allowed.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)))
        {
            return new Outcome("pass", "Тип детали " + name);
        }

        return new Outcome("fail", "Тип детали " + name + ", ожидается " + string.Join(" / ", allowed));
    }

    private static string Describe(ConnectorElement c)
    {
        var o = c.Origin;
        var z = c.CoordinateSystem.BasisZ;
        var linked = c.GetLinkedConnectorElement();
        var diameter = c.get_Parameter(BuiltInParameter.CONNECTOR_DIAMETER);
        return "коннектор " + c.Id + ": Ø " + (diameter == null ? "-" : Math.Round(diameter.AsDouble() * 304.8, 1).ToString()) +
               ", точка [" + Math.Round(o.X * 304.8, 2) + ", " + Math.Round(o.Y * 304.8, 2) + ", " + Math.Round(o.Z * 304.8, 2) + "] мм" +
               ", направление [" + Vec(z) + "]" + (c.IsPrimary ? ", главный" : "") + (linked == null ? ", без связи" : ", связан с " + linked.Id);
    }

    private static string Vec(XYZ v)
    {
        return Math.Round(v.X, 3) + ", " + Math.Round(v.Y, 3) + ", " + Math.Round(v.Z, 3);
    }
}
