// description: Документы, открытые в Revit: проект/семейство, имя, путь, есть ли несохранённые изменения; первым — версия Revit и активный документ
// parameters:
// transaction: none

// Перед пакетной правкой .rfa: открытые пользователем семейства правятся на месте, а не открываются второй раз.
var app = document.Application;
var lines = new List<string> { "Revit " + app.VersionNumber + ", активный: " + document.Title };
foreach (Document d in app.Documents)
    lines.Add((d.IsFamilyDocument ? "семейство | " : "проект | ") + d.Title + " | " + d.PathName + " | изменён: " + (d.IsModified ? "да" : "нет"));
return string.Join("\n", lines);
