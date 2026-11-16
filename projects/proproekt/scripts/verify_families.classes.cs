// Проверка готовых семейств ПроПроекта по требованиям: файл и версия, имя, категория, три параметра ADSK
// (общие, GUID из ФОП, тип/экземпляр, значения), один типоразмер, габарит, точка вставки, запрещённая геометрия.
// Плюс сетка треугольников для картинок (изометрия и проекции рисуются вне Revit).
public static class ProVerify
{
    const double Mm = 304.8;

    static bool IsHost(GenericForm f)
    {
        var bb = f.get_BoundingBox(null);
        if (bb == null || !(f is Extrusion)) return false;
        var s = bb.Max - bb.Min;
        return Math.Abs(s.X * Mm - 2438.4) < 0.2 && Math.Abs(s.Y * Mm - 2438.4) < 0.2 && Math.Abs(s.Z * Mm - 304.8) < 0.2;
    }

    static double R(double feet) { return Math.Round(feet * Mm, 1); }

    /// <summary>Точки ребра для габарита: разбивка Revit пропускает крайние точки окружностей (до 3 мм на Ø560).</summary>
    static IEnumerable<XYZ> EdgePoints(Edge edge)
    {
        var curve = edge.AsCurve();
        if (curve is Line) return new[] { curve.GetEndPoint(0), curve.GetEndPoint(1) };
        return Enumerable.Range(0, 181).Select(i => curve.Evaluate(i / 180.0, true));
    }

    public static JObject Check(Autodesk.Revit.ApplicationServices.Application app, JObject job, Dictionary<string, Guid> fopGuids, string imagesDir)
    {
        var path = (string)job["output"];
        var family = (string)job["family"];
        var r = new JObject { ["family"] = family, ["path"] = path };
        if (!File.Exists(path)) { r["error"] = "файла нет"; return r; }
        r["savedInVersion"] = BasicFileInfo.Extract(path).SavedInVersion;
        var doc = app.OpenDocumentFile(path);
        try
        {
            r["title"] = doc.Title;
            var cat = doc.OwnerFamily.FamilyCategory;
            r["category"] = cat.Name;
            r["builtInCategory"] = ((BuiltInCategory)cat.Id.IntegerValue).ToString();
            var fm = doc.FamilyManager;
            r["types"] = new JArray(fm.Types.Cast<FamilyType>().Select(t => (object)t.Name).ToArray());
            var pars = new JObject();
            foreach (var name in new[] { "ADSK_Наименование", "ADSK_Марка", "ADSK_Позиция" })
            {
                var fp = fm.get_Parameter(name);
                if (fp == null) { pars[name] = null; continue; }
                Guid want;
                fopGuids.TryGetValue(name, out want);
                var guid = fp.IsShared ? fp.GUID : Guid.Empty;
                pars[name] = new JObject
                {
                    ["shared"] = fp.IsShared,
                    ["guid"] = guid.ToString(),
                    ["guidMatchesFop"] = fp.IsShared && guid == want,
                    ["instance"] = fp.IsInstance,
                    ["group"] = fp.Definition.ParameterGroup.ToString(),
                    ["value"] = fm.CurrentType == null ? null : fm.CurrentType.AsString(fp)
                };
            }
            r["adsk"] = pars;
            r["otherAdsk"] = new JArray(fm.Parameters.Cast<FamilyParameter>().Select(p => p.Definition.Name)
                .Where(n => n.StartsWith("ADSK_") && n != "ADSK_Наименование" && n != "ADSK_Марка" && n != "ADSK_Позиция").Cast<object>().ToArray());

            var forms = new FilteredElementCollector(doc).OfClass(typeof(GenericForm)).Cast<GenericForm>().Where(f => !IsHost(f)).ToList();
            var solids = forms.Where(f => f.IsSolid).ToList();
            // Геометрия с вырезанными пустотами принадлежит объединениям (GeomCombination), а не формам.
            var geometry = solids.Cast<Element>().Concat(new FilteredElementCollector(doc).OfClass(typeof(GeomCombination))).ToList();
            r["solidForms"] = solids.Count;
            r["voidForms"] = forms.Count - solids.Count;
            r["placement"] = doc.OwnerFamily.FamilyPlacementType.ToString();
            XYZ min = null, max = null;
            double volume = 0;
            var options = new Options { DetailLevel = ViewDetailLevel.Fine };
            foreach (var f in geometry)
            {
                foreach (var obj in f.get_Geometry(options))
                {
                    var s = obj as Solid;
                    if (s == null || s.Volume <= 0) continue;
                    volume += s.Volume;
                    // Габарит — по вершинам рёбер с мелкой разбивкой: GetBoundingBox для дуг даёт неточную оболочку.
                    foreach (Edge edge in s.Edges)
                        foreach (var p in EdgePoints(edge))
                        {
                            min = min == null ? p : new XYZ(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                            max = max == null ? p : new XYZ(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
                        }
                }
            }
            if (min != null)
            {
                r["size"] = new JArray(R(max.X - min.X), R(max.Y - min.Y), R(max.Z - min.Z));
                r["min"] = new JArray(R(min.X), R(min.Y), R(min.Z));
                r["max"] = new JArray(R(max.X), R(max.Y), R(max.Z));
                r["center"] = new JArray(R((min.X + max.X) / 2), R((min.Y + max.Y) / 2), R(min.Z));
                r["volumeL"] = Math.Round(volume * 28.316846592, 3);
            }
            r["imports"] = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).GetElementCount();
            r["freeForms"] = new FilteredElementCollector(doc).OfClass(typeof(FreeFormElement)).GetElementCount();
            r["directShapes"] = new FilteredElementCollector(doc).OfClass(typeof(DirectShape)).GetElementCount();
            r["nestedFamilies"] = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).GetElementCount();
            if (imagesDir != null) r["mesh"] = Mesh(geometry, family, imagesDir);
        }
        finally
        {
            doc.Close(false);
        }
        return r;
    }

    /// <summary>Треугольники твёрдой геометрии (мм) для картинок проверки: ExportImage в Revit 2019 открывает документ в окне.</summary>
    static string Mesh(List<Element> solids, string family, string dir)
    {
        Directory.CreateDirectory(dir);
        var tris = new JArray();
        var options = new Options { DetailLevel = ViewDetailLevel.Fine };
        foreach (var f in solids)
            foreach (var obj in f.get_Geometry(options))
            {
                var s = obj as Solid;
                if (s == null || s.Volume <= 0) continue;
                foreach (Face face in s.Faces)
                {
                    var m = face.Triangulate(0.3);
                    for (var i = 0; i < m.NumTriangles; i++)
                    {
                        var t = m.get_Triangle(i);
                        var row = new JArray();
                        for (var k = 0; k < 3; k++)
                        {
                            var v = t.get_Vertex(k);
                            row.Add(R(v.X)); row.Add(R(v.Y)); row.Add(R(v.Z));
                        }
                        tris.Add(row);
                    }
                }
            }
        var target = Path.Combine(dir, family + ".json");
        File.WriteAllText(target, tris.ToString(Newtonsoft.Json.Formatting.None), new System.Text.UTF8Encoding(false));
        return target;
    }

    public static Dictionary<string, Guid> FopGuids(Autodesk.Revit.ApplicationServices.Application app, string fop)
    {
        var result = new Dictionary<string, Guid>();
        var previous = app.SharedParametersFilename;
        app.SharedParametersFilename = fop;
        try
        {
            var file = app.OpenSharedParameterFile();
            foreach (DefinitionGroup g in file.Groups)
                foreach (Definition d in g.Definitions)
                {
                    var e = d as ExternalDefinition;
                    if (e != null && !result.ContainsKey(e.Name)) result[e.Name] = e.GUID;
                }
        }
        finally
        {
            try { app.SharedParametersFilename = previous ?? ""; } catch { }
        }
        return result;
    }
}
