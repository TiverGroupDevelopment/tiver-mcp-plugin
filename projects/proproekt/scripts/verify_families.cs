// description: Проверяет готовые семейства ПроПроекта (.rfa из jobs.json): версия 2019, имя, категория, три параметра ADSK с GUID из ФОП, типоразмер, габарит, точка вставки, запрещённая геометрия; выгружает сетку треугольников для картинок
// parameters: jobsPath, fopPath, proofDir, from, count
// transaction: none
// usings: using System.IO; using Newtonsoft.Json; using Newtonsoft.Json.Linq;

// Результат — строки JSON в <proofDir>/verify.jsonl (дописываются), сетки — <proofDir>/сетки/<семейство>.json.
var jobs = JArray.Parse(File.ReadAllText((string)parameters[0]));
var app = document.Application;
var guids = ProVerify.FopGuids(app, (string)parameters[1]);
var proofDir = (string)parameters[2];
var from = int.Parse((string)parameters[3]);
var count = int.Parse((string)parameters[4]);
Directory.CreateDirectory(proofDir);
var lines = new List<string>();
for (var i = from; i < Math.Min(jobs.Count, from + count); i++)
{
    var job = (JObject)jobs[i];
    JObject r;
    try { r = ProVerify.Check(app, job, guids, Path.Combine(proofDir, "сетки")); }
    catch (Exception ex) { r = new JObject { ["family"] = job["family"], ["error"] = ex.Message }; }
    r["index"] = i;
    File.AppendAllText(Path.Combine(proofDir, "verify.jsonl"), r.ToString(Newtonsoft.Json.Formatting.None) + "\n", new System.Text.UTF8Encoding(false));
    lines.Add(i + " " + job["family"] + (r["error"] != null ? " ОШИБКА " + r["error"] : " " + r["size"]));
}
return string.Join("\n", lines);
