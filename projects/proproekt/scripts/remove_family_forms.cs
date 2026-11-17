// description: Удаляет из семейства формы, чей габарит целиком лежит в одной из заданных зон (мм), и сохраняет .rfa; dryRun только показывает, что удалится
// parameters: family, zones, dryRun
// transaction: none

// family — начало имени открытого документа («1.24_») или путь к .rfa (откроется, сохранится и закроется).
// zones — зоны через «;», каждая «xMin,xMax,yMin,yMax,zMin,zMax» в мм от начала семейства; пустое значение — без ограничения.
//   Подлокотники кресла 1.24: «272,,,,400,;,-272,,,400,» — всё правее x=272 и левее x=−272 выше 400 мм.
// dryRun — «true»: ничего не удалять, вернуть список форм под удаление (сначала смотреть list_family_forms).
var key = (string)parameters[0];
var dryRun = string.Equals((string)parameters[2], "true", StringComparison.OrdinalIgnoreCase);
const double mm = 304.8;
var zones = new List<double[]>();
foreach (var z in ((string)parameters[1]).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
{
    var p = z.Split(',');
    if (p.Length != 6) return "зона должна иметь 6 значений: " + z;
    var v = new double[6];
    for (var i = 0; i < 6; i++)
        v[i] = p[i].Trim() == "" ? (i % 2 == 0 ? double.NegativeInfinity : double.PositiveInfinity)
                                 : double.Parse(p[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
    zones.Add(v);
}
if (zones.Count == 0) return "зоны не заданы";

var app = document.Application;
Document doc = null;
foreach (Document d in app.Documents)
    if (d.IsFamilyDocument && (d.Title.StartsWith(key) || string.Equals(d.PathName, key, StringComparison.OrdinalIgnoreCase))) doc = d;
var opened = false;
if (doc == null && key.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase)) { doc = app.OpenDocumentFile(key); opened = true; }
if (doc == null) return "семейство не найдено: " + key;
try
{
    var ids = new List<ElementId>();
    var lines = new List<string>();
    foreach (var f in new FilteredElementCollector(doc).OfClass(typeof(GenericForm)).Cast<GenericForm>())
    {
        var bb = f.get_BoundingBox(null);
        if (bb == null) continue;
        double x0 = bb.Min.X * mm, x1 = bb.Max.X * mm, y0 = bb.Min.Y * mm, y1 = bb.Max.Y * mm, z0 = bb.Min.Z * mm, z1 = bb.Max.Z * mm;
        foreach (var v in zones)
            if (x0 >= v[0] && x1 <= v[1] && y0 >= v[2] && y1 <= v[3] && z0 >= v[4] && z1 <= v[5])
            {
                ids.Add(f.Id);
                lines.Add(string.Format("{0} {1} x[{2:0},{3:0}] y[{4:0},{5:0}] z[{6:0},{7:0}]", f.Id.IntegerValue, f.GetType().Name, x0, x1, y0, y1, z0, z1));
                break;
            }
    }
    if (!dryRun && ids.Count > 0)
    {
        using (var t = new Transaction(doc, "Удалить формы"))
        {
            t.Start();
            doc.Delete(ids);
            t.Commit();
        }
        doc.Save();
    }
    var left = new FilteredElementCollector(doc).OfClass(typeof(GenericForm)).GetElementCount();
    return (dryRun ? "удалится " : "удалено ") + ids.Count + ", форм " + (dryRun ? "сейчас " : "осталось ") + left + "\n" + string.Join("\n", lines);
}
finally { if (opened) doc.Close(false); }
