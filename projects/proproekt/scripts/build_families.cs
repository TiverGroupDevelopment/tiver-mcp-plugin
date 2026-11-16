// description: Строит семейства ПроПроекта в Revit 2019 по заданиям jobs.json (кубик, цилиндр или перенос исходника по эскизам), добавляет ADSK_Наименование/Марка/Позиция и сохраняет .rfa
// parameters: jobsPath, fopPath, from, count, logPath
// transaction: none
// usings: using System.IO; using Newtonsoft.Json; using Newtonsoft.Json.Linq;

// jobsPath — jobs.json (tools/make_jobs.py); fopPath — ФОП проекта; from/count — диапазон заданий;
// logPath — JSONL с результатом по каждому семейству (дописывается, даже если ответ не дошёл до агента).
var jobs = JArray.Parse(File.ReadAllText((string)parameters[0]));
var fop = (string)parameters[1];
var from = int.Parse((string)parameters[2]);
var count = int.Parse((string)parameters[3]);
var logPath = (string)parameters[4];
var lines = new List<string>();
for (var i = from; i < Math.Min(jobs.Count, from + count); i++)
{
    var job = (JObject)jobs[i];
    JObject result;
    try { result = ProBuild.Build(document, job, fop); }
    catch (Exception ex) { result = new JObject { ["family"] = job["family"], ["ok"] = false, ["error"] = ex.Message }; }
    result["index"] = i;
    File.AppendAllText(logPath, result.ToString(Newtonsoft.Json.Formatting.None) + "\n", new System.Text.UTF8Encoding(false));
    lines.Add(i + " " + ((bool)result["ok"] ? "OK " : "ОШИБКА ") + result["family"] + " " + (result["forms"] ?? "") + " " + (result["error"] ?? ""));
}
return string.Join("\n", lines);
