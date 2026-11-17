// description: Делает ADSK_Позиция параметром типа в готовых .rfa ПроПроекта (значение — позиция из jobs.json) и сохраняет файлы; уже открытые документы правит на месте
// parameters: jobsPath, from, count
// transaction: none
// usings: using System.IO; using Newtonsoft.Json.Linq;

var jobs = JArray.Parse(File.ReadAllText((string)parameters[0]));
var from = int.Parse((string)parameters[1]);
var count = int.Parse((string)parameters[2]);
var app = document.Application;
var lines = new List<string>();
for (var i = from; i < Math.Min(jobs.Count, from + count); i++)
{
    var job = (JObject)jobs[i];
    var path = (string)job["output"];
    var position = (string)job["position"];
    Document doc = null;
    foreach (Document d in app.Documents)
        if (string.Equals(d.PathName, path, StringComparison.OrdinalIgnoreCase)) doc = d;
    var wasOpen = doc != null;
    try
    {
        if (doc == null) doc = app.OpenDocumentFile(path);
        var fm = doc.FamilyManager;
        var fp = fm.get_Parameter("ADSK_Позиция");
        if (fp == null) { lines.Add(i + " НЕТ ПАРАМЕТРА " + job["family"]); continue; }
        var was = fp.IsInstance ? "экз" : "тип";
        using (var t = new Transaction(doc, "ADSK_Позиция — параметр типа"))
        {
            t.Start();
            if (fp.IsInstance) fm.MakeType(fp);
            fm.Set(fp, position);
            t.Commit();
        }
        var now = fm.CurrentType.AsString(fp);
        doc.Save();
        lines.Add(i + " OK " + was + "→" + (fp.IsInstance ? "экз" : "тип") + " «" + now + "» " + (wasOpen ? "(открыт) " : "") + job["family"]);
    }
    catch (Exception ex) { lines.Add(i + " ОШИБКА " + job["family"] + ": " + ex.Message); }
    finally { if (!wasOpen && doc != null) doc.Close(false); }
}
return string.Join("\n", lines);
