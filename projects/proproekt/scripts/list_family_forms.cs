// description: Формы семейства (выдавливания, сдвиги, вращения, сопряжения) с Id, видом, твёрдая/пустотная и габаритом в мм; семейство — открытый документ (начало имени) или путь к .rfa
// parameters: family
// transaction: none

// family — начало имени открытого документа («1.24_») или полный путь к .rfa (откроется и закроется без сохранения).
// По габаритам видно, какие формы удалять командой remove_family_forms (например, подлокотники кресла).
var key = (string)parameters[0];
var app = document.Application;
Document doc = null;
foreach (Document d in app.Documents)
    if (d.IsFamilyDocument && (d.Title.StartsWith(key) || string.Equals(d.PathName, key, StringComparison.OrdinalIgnoreCase))) doc = d;
var opened = false;
if (doc == null && key.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase)) { doc = app.OpenDocumentFile(key); opened = true; }
if (doc == null) return "семейство не найдено: " + key;
try
{
    const double mm = 304.8;
    var lines = new List<string> { doc.PathName };
    foreach (var f in new FilteredElementCollector(doc).OfClass(typeof(GenericForm)).Cast<GenericForm>())
    {
        var bb = f.get_BoundingBox(null);
        if (bb == null) continue;
        lines.Add(string.Format("{0} {1} {2} x[{3:0},{4:0}] y[{5:0},{6:0}] z[{7:0},{8:0}]", f.Id.IntegerValue, f.GetType().Name, f.IsSolid ? "тв" : "пуст",
            bb.Min.X * mm, bb.Max.X * mm, bb.Min.Y * mm, bb.Max.Y * mm, bb.Min.Z * mm, bb.Max.Z * mm));
    }
    lines.Add("форм: " + (lines.Count - 1));
    return string.Join("\n", lines);
}
finally { if (opened) doc.Close(false); }
