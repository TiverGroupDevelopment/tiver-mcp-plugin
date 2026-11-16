// description: Снимает данные исходных семейств из открытого проекта (Revit 2025/2026) для пересборки в 2019: family.json и копия .rfa на каждое семейство
// parameters: outDir, familyNames
// transaction: none
// usings: using System.IO; using Newtonsoft.Json; using Newtonsoft.Json.Linq;

// outDir — папка результатов, внутри неё по папке на семейство.
// familyNames — имена семейств проекта через «|»; пусто — все загружаемые семейства модели.
var outDir = (string)parameters[0];
var wanted = parameters.Length > 1 && !string.IsNullOrWhiteSpace((string)parameters[1])
    ? ((string)parameters[1]).Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToList()
    : null;
Directory.CreateDirectory(outDir);

var families = new FilteredElementCollector(document).OfClass(typeof(Family)).Cast<Family>()
    .Where(f => !f.IsInPlace && f.IsEditable && f.FamilyCategory != null && f.FamilyCategory.CategoryType == CategoryType.Model)
    .ToList();
var report = new List<string>();
var targets = wanted == null ? families : wanted.Select(n => families.FirstOrDefault(f => f.Name == n)).ToList();
for (var i = 0; i < targets.Count; i++)
{
    var fam = targets[i];
    if (fam == null) { report.Add("НЕТ В ПРОЕКТЕ: " + wanted[i]); continue; }
    var dir = Path.Combine(outDir, SourceFamilyDump.SafeName(fam.Name));
    Directory.CreateDirectory(dir);
    Document fd = null;
    try
    {
        fd = document.EditFamily(fam);
        var json = SourceFamilyDump.Dump(fd, 0);
        json["familyName"] = fam.Name;
        json["sourceProject"] = document.PathName;
        json["revitVersion"] = document.Application.VersionNumber;
        var symbols = fam.GetFamilySymbolIds().Select(id => document.GetElement(id)).OfType<FamilySymbol>().ToList();
        var instances = new FilteredElementCollector(document).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
            .Where(x => x.Symbol.Family.Id == fam.Id).ToList();
        json["projectInstances"] = instances.Count;
        json["projectTypes"] = new JArray(symbols.Select(s => (object)new JObject
        {
            ["name"] = s.Name,
            ["instances"] = instances.Count(x => x.Symbol.Id == s.Id)
        }).ToArray());
        var hosts = instances.Where(x => x.Host != null).Select(x => x.Host.Category != null ? x.Host.Category.Name : "host").Distinct().ToList();
        if (hosts.Count > 0) json["projectHosts"] = new JArray(hosts.Cast<object>().ToArray());
        File.WriteAllText(Path.Combine(dir, "family.json"), json.ToString(Newtonsoft.Json.Formatting.Indented), new System.Text.UTF8Encoding(false));
        fd.SaveAs(Path.Combine(dir, SourceFamilyDump.SafeName(fam.Name) + "_R" + document.Application.VersionNumber + ".rfa"),
            new SaveAsOptions { OverwriteExistingFile = true });
        report.Add("OK: " + fam.Name + " | " + json["category"] + " | " + json["formSummary"] + " | вложенных " + ((JArray)json["nestedFamilies"]).Count);
    }
    catch (Exception ex) { report.Add("ОШИБКА: " + fam.Name + " — " + ex.Message); }
    finally { if (fd != null) fd.Close(false); }
}
return string.Join("\n", report);
