// Построение семейств ПроПроекта в Revit 2019 по заданиям jobs.json (tools/make_jobs.py).
// Координаты в заданиях и снимках — мм; в Revit — футы.

public class ProSilentFailures : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
    {
        var error = false;
        foreach (var message in accessor.GetFailureMessages())
        {
            if (message.GetSeverity() == FailureSeverity.Warning) accessor.DeleteWarning(message);
            else error = true;
        }
        if (!error) return FailureProcessingResult.Continue;
        var options = accessor.GetFailureHandlingOptions();
        options.SetClearAfterRollback(true);
        accessor.SetFailureHandlingOptions(options);
        return FailureProcessingResult.ProceedWithRollBack;
    }
}

public static class ProBuild
{
    public const double Mm = 304.8;

    public static XYZ P(JToken a) { return new XYZ((double)a[0], (double)a[1], (double)a[2]); }
    public static XYZ Ft(XYZ mm) { return new XYZ(mm.X / Mm, mm.Y / Mm, mm.Z / Mm); }

    /// <summary>Транзакция с подавлением предупреждений; ошибка Revit — откат без окна.</summary>
    public static bool Run(Document doc, string name, Action action, out string error)
    {
        error = null;
        using (var t = new Transaction(doc, name))
        {
            var options = t.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new ProSilentFailures());
            options.SetClearAfterRollback(true);
            t.SetFailureHandlingOptions(options);
            t.Start();
            try { action(); }
            catch (Exception ex)
            {
                error = ex.Message;
                if (t.GetStatus() == TransactionStatus.Started) t.RollBack();
                return false;
            }
            var status = t.Commit();
            if (status != TransactionStatus.Committed) { error = "Revit откатил: " + status; return false; }
            return true;
        }
    }

    // ---------- кривые ----------

    static void AddLine(List<Curve> res, XYZ a, XYZ b)
    {
        if (a.DistanceTo(b) > 0.8) res.Add(Line.CreateBound(Ft(a), Ft(b)));
    }

    static void AddClosed(List<Curve> res, XYZ center, XYZ xdir, XYZ ydir, double rx, double ry, Func<XYZ, XYZ> map)
    {
        var mc = map(center);
        var mu = map(center + xdir * rx) - mc;
        var mv = map(center + ydir * ry) - mc;
        double lu = mu.GetLength(), lv = mv.GetLength();
        if (Math.Abs(mu.DotProduct(mv)) < 1e-4 * lu * lv)
        {
            var ax = mu.Normalize();
            var ay = mv.Normalize();
            if (Math.Abs(lu - lv) < 0.005 * Math.Max(lu, lv))
            {
                var r = (lu + lv) / 2 / Mm;
                res.Add(Arc.Create(Ft(mc), r, 0, Math.PI, ax, ay));
                res.Add(Arc.Create(Ft(mc), r, Math.PI, 2 * Math.PI, ax, ay));
            }
            else
            {
                res.Add(Ellipse.CreateCurve(Ft(mc), lu / Mm, lv / Mm, ax, ay, 0, Math.PI));
                res.Add(Ellipse.CreateCurve(Ft(mc), lu / Mm, lv / Mm, ax, ay, Math.PI, 2 * Math.PI));
            }
            return;
        }
        // Оси после преобразования не перпендикулярны — многоугольник.
        const int n = 32;
        XYZ prev = null;
        for (var i = 0; i <= n; i++)
        {
            var a = 2 * Math.PI * i / n;
            var p = map(center + xdir * (rx * Math.Cos(a)) + ydir * (ry * Math.Sin(a)));
            if (prev != null) AddLine(res, prev, p);
            prev = p;
        }
    }

    /// <summary>Масштаб в плоскости дуги неравномерный: дуга по трём точкам выпирает за аффинный образ.</summary>
    static bool Anisotropic(JObject c, Func<XYZ, XYZ> map)
    {
        var center = P(c["center"]);
        var r = (double)c["r"];
        var n = P(c["normal"]).Normalize();
        var u = (P(c["p0"]) - center).Normalize();
        var v = n.CrossProduct(u);
        var mc = map(center);
        var mu = map(center + u * r) - mc;
        var mv = map(center + v * r) - mc;
        double lu = mu.GetLength(), lv = mv.GetLength();
        return Math.Abs(lu - lv) > 0.01 * Math.Max(lu, lv) || Math.Abs(mu.DotProduct(mv)) > 1e-3 * lu * lv;
    }

    static void AddArcPolyline(List<Curve> res, JObject c, Func<XYZ, XYZ> map)
    {
        var center = P(c["center"]);
        var r = (double)c["r"];
        var n = P(c["normal"]).Normalize();
        var u = (P(c["p0"]) - center).Normalize();
        var v = n.CrossProduct(u);
        Func<XYZ, double> angle = p =>
        {
            var d = p - center;
            var a = Math.Atan2(d.DotProduct(v), d.DotProduct(u));
            return a <= 1e-9 ? a + 2 * Math.PI : a;
        };
        var end = angle(P(c["p1"]));
        var mid = angle(P(c["mid"]));
        if (mid > end) { v = v.Negate(); end = angle(P(c["p1"])); }
        var length = map(P(c["p0"])).DistanceTo(map(P(c["mid"]))) + map(P(c["mid"])).DistanceTo(map(P(c["p1"])));
        var count = Math.Max(2, Math.Min(24, (int)Math.Ceiling(length / 6.0)));
        var prev = map(P(c["p0"]));
        for (var i = 1; i <= count; i++)
        {
            var p = i == count ? map(P(c["p1"])) : map(center + u * (r * Math.Cos(end * i / count)) + v * (r * Math.Sin(end * i / count)));
            if (prev.DistanceTo(p) > 0.8 || i == count)
            {
                AddLine(res, prev, p);
                prev = p;
            }
        }
    }

    public static List<Curve> Curves(JObject c, Func<XYZ, XYZ> map)
    {
        var res = new List<Curve>();
        var t = (string)c["t"];
        var closed = c["closed"] != null && (bool)c["closed"];
        if (t == "line")
        {
            AddLine(res, map(P(c["p0"])), map(P(c["p1"])));
        }
        else if (t == "arc" && !closed && Anisotropic(c, map))
        {
            AddArcPolyline(res, c, map);
        }
        else if ((t == "arc" || t == "ellipse") && !closed)
        {
            XYZ a = map(P(c["p0"])), b = map(P(c["p1"])), m = map(P(c["mid"]));
            try
            {
                if (a.DistanceTo(b) > 0.8) res.Add(Arc.Create(Ft(a), Ft(b), Ft(m)));
            }
            catch
            {
                AddLine(res, a, m);
                AddLine(res, m, b);
            }
        }
        else if (t == "arc")
        {
            var normal = P(c["normal"]).Normalize();
            var xdir = P(c["xdir"]).Normalize();
            var r = (double)c["r"];
            AddClosed(res, P(c["center"]), xdir, normal.CrossProduct(xdir), r, r, map);
        }
        else if (t == "ellipse")
        {
            AddClosed(res, P(c["center"]), P(c["xdir"]).Normalize(), P(c["ydir"]).Normalize(), (double)c["rx"], (double)c["ry"], map);
        }
        else if (c["pts"] != null)
        {
            var pts = ((JArray)c["pts"]).Select(x => map(P(x))).ToList();
            for (var i = 0; i + 1 < pts.Count; i++) AddLine(res, pts[i], pts[i + 1]);
        }
        return res;
    }

    public static CurveArrArray Profile(JToken loops, Func<XYZ, XYZ> map)
    {
        var arr = new CurveArrArray();
        foreach (JArray loop in loops)
        {
            var ca = new CurveArray();
            foreach (JObject c in loop) foreach (var cv in Curves(c, map)) ca.Append(cv);
            if (ca.Size > 0) arr.Append(ca);
        }
        return arr;
    }

    /// <summary>Точки — точно на плоскость эскиза: снимок округлён до 0,01 мм, а Revit требует точного попадания (наклонные плоскости).</summary>
    public static Func<XYZ, XYZ> OnPlane(Func<XYZ, XYZ> map, Plane plane)
    {
        var o = plane.Origin * Mm;
        var n = plane.Normal;
        return p => { var q = map(p); return q - n * (q - o).DotProduct(n); };
    }

    /// <summary>Плоскость эскиза после преобразования; w — образ единичной нормали (мм), для пересчёта отступов.</summary>
    public static Plane MapPlane(JObject pl, Func<XYZ, XYZ> map, double shift, out XYZ w)
    {
        var o = P(pl["origin"]);
        var n = P(pl["normal"]).Normalize();
        var u = P(pl["xvec"]).Normalize();
        var v = n.CrossProduct(u);
        o = o + n * shift;
        var mo = map(o);
        var mu = map(o + u * 100) - mo;
        var mv = map(o + v * 100) - mo;
        var nn = mu.CrossProduct(mv).Normalize();
        w = map(o + n) - mo;
        if (w.DotProduct(nn) < 0) nn = nn.Negate();
        return Plane.CreateByNormalAndOrigin(nn, Ft(mo));
    }

    // ---------- формы ----------

    public static Element Extrusion(Document doc, JObject f, Func<XYZ, XYZ> map)
    {
        var sk = (JObject)f["sketch"];
        XYZ w;
        var pl = MapPlane((JObject)sk["plane"], map, 0, out w);
        var k = w.DotProduct(pl.Normal);
        var profile = Profile(sk["loops"], OnPlane(map, pl));
        if (profile.Size == 0) throw new Exception("пустой эскиз");
        var sp = SketchPlane.Create(doc, pl);
        double s = (double)f["start"] * k / Mm, e = (double)f["end"] * k / Mm;
        if (Math.Abs(e - s) < 0.5 / Mm) e = s + 0.5 / Mm;
        // NewExtrusion строит от плоскости эскиза на положительную длину; начало и конец (в т.ч. начало > конца) — после.
        var ex = doc.FamilyCreate.NewExtrusion((bool)f["solid"], profile, sp, Math.Abs(e - s));
        try { ex.StartOffset = s; ex.EndOffset = e; }
        catch { ex.EndOffset = e; ex.StartOffset = s; }
        return ex;
    }

    public static Element Revolution(Document doc, JObject f, Func<XYZ, XYZ> map)
    {
        var sk = (JObject)f["sketch"];
        XYZ w;
        var pl = MapPlane((JObject)sk["plane"], map, 0, out w);
        var profile = Profile(sk["loops"], OnPlane(map, pl));
        var axis = Curves((JObject)f["axis"], OnPlane(map, pl)).OfType<Line>().FirstOrDefault();
        if (axis == null) throw new Exception("нет оси вращения");
        var sp = SketchPlane.Create(doc, pl);
        var a0 = (double)f["startAngle"] * Math.PI / 180;
        var a1 = (double)f["endAngle"] * Math.PI / 180;
        return doc.FamilyCreate.NewRevolution((bool)f["solid"], profile, sp, axis, a0, a1);
    }

    public static Element Blend(Document doc, JObject f, Func<XYZ, XYZ> map)
    {
        var bottom = (JObject)f["bottom"];
        var top = (JObject)f["top"];
        var n = P(bottom["plane"]["normal"]).Normalize();
        var bo = (double)f["bottomOffset"];
        var to = (double)f["topOffset"];
        Func<XYZ, XYZ> mb = p => map(p + n * bo);
        Func<XYZ, XYZ> mt = p => map(p + n * to);
        XYZ w;
        var pl = MapPlane((JObject)bottom["plane"], map, bo, out w);
        var topPl = MapPlane((JObject)bottom["plane"], map, to, out w);
        var baseProfile = Profile(bottom["loops"], OnPlane(mb, pl));
        var topProfile = Profile(top["loops"], OnPlane(mt, topPl));
        if (baseProfile.Size == 0 || topProfile.Size == 0) throw new Exception("пустой контур сопряжения");
        var sp = SketchPlane.Create(doc, pl);
        return doc.FamilyCreate.NewBlend((bool)f["solid"], topProfile.get_Item(0), baseProfile.get_Item(0), sp);
    }

    static List<Curve> PathCurves(JObject path, Func<XYZ, XYZ> map)
    {
        var list = new List<Curve>();
        foreach (JArray loop in path["loops"]) foreach (JObject c in loop) list.AddRange(Curves(c, map));
        return list;
    }

    public static Element Sweep(Document doc, JObject f, Func<XYZ, XYZ> map)
    {
        var path = f["path"] as JObject;
        var profileSk = f["profile"] as JObject;
        if (path == null || profileSk == null) throw new Exception("сдвиг по 3D-пути или профилем-семейством");
        XYZ w;
        var pathPlane = MapPlane((JObject)path["plane"], map, 0, out w);
        var curves = PathCurves(path, OnPlane(map, pathPlane));
        var ca = new CurveArray();
        foreach (var c in curves) ca.Append(c);
        var profPlane = MapPlane((JObject)profileSk["plane"], map, 0, out w);
        var profile = Profile(profileSk["loops"], OnPlane(map, profPlane));
        int index;
        ProfilePlaneLocation location;
        FindProfileLocation(curves, profPlane, out index, out location);
        var prm = location == ProfilePlaneLocation.Start ? 0.0 : location == ProfilePlaneLocation.MidPoint ? 0.5 : 1.0;
        var local = ToProfileLocal(profile, curves[index], prm, pathPlane.Normal, profPlane);
        var sweepProfile = doc.Application.Create.NewCurveLoopsProfile(local);
        var sp = SketchPlane.Create(doc, pathPlane);
        return doc.FamilyCreate.NewSweep((bool)f["solid"], ca, sp, sweepProfile, index, location);
    }

    /// <summary>
    /// Профиль сдвига в локальных осях Revit: начало — точка пути, X = N×T, Y = N (N — нормаль плоскости пути,
    /// T — касательная пути). Так NewSweep / NewSweptBlend ставят профиль (проверено опытом в Revit 2019).
    /// </summary>
    public static CurveArrArray ToProfileLocal(CurveArrArray model, Curve pathCurve, double prm, XYZ pathNormal, Plane profPlane)
    {
        var point = pathCurve.Evaluate(prm, true);
        var tangent = pathCurve.ComputeDerivatives(prm, true).BasisX.Normalize();
        var x = pathNormal.CrossProduct(tangent).Normalize();
        var z = profPlane.Normal;
        var xp = (x - z * x.DotProduct(z)).Normalize();
        var yp = z.CrossProduct(xp);
        if (yp.DotProduct(pathNormal) < 0) { z = z.Negate(); yp = z.CrossProduct(xp); }
        var origin = point - z * (point - profPlane.Origin).DotProduct(z);
        var frame = Transform.Identity;
        frame.Origin = origin;
        frame.BasisX = xp;
        frame.BasisY = yp;
        frame.BasisZ = z;
        var inverse = frame.Inverse;
        var result = new CurveArrArray();
        foreach (CurveArray loop in model)
        {
            var ca = new CurveArray();
            foreach (Curve c in loop) ca.Append(c.CreateTransformed(inverse));
            result.Append(ca);
        }
        return result;
    }

    static double DistToPlane(XYZ p, Plane pl) { return Math.Abs((p - pl.Origin).DotProduct(pl.Normal)); }

    public static bool FindProfileLocation(List<Curve> curves, Plane profPlane, out int index, out ProfilePlaneLocation location)
    {
        var options = new[] { ProfilePlaneLocation.Start, ProfilePlaneLocation.MidPoint, ProfilePlaneLocation.End };
        var param = new[] { 0.0, 0.5, 1.0 };
        for (var i = 0; i < curves.Count; i++)
        {
            for (var k = 0; k < 3; k++)
            {
                if (DistToPlane(curves[i].Evaluate(param[k], true), profPlane) < 1.0 / Mm)
                {
                    index = i;
                    location = options[k];
                    return true;
                }
            }
        }
        index = 0;
        location = ProfilePlaneLocation.Start;
        return false;
    }

    public static Element SweptBlend(Document doc, JObject f, Func<XYZ, XYZ> map)
    {
        var path = f["path"] as JObject;
        var bottom = f["bottom"] as JObject;
        var top = f["top"] as JObject;
        if (path == null || bottom == null || top == null) throw new Exception("сдвиг-сопряжение без эскизов");
        XYZ w;
        var pathPlane = MapPlane((JObject)path["plane"], map, 0, out w);
        var curves = PathCurves(path, OnPlane(map, pathPlane));
        if (curves.Count != 1) throw new Exception("путь сдвига-сопряжения не из одной кривой");
        var bottomPlane = MapPlane((JObject)bottom["plane"], map, 0, out w);
        var topPlane = MapPlane((JObject)top["plane"], map, 0, out w);
        var bp = doc.Application.Create.NewCurveLoopsProfile(ToProfileLocal(Profile(bottom["loops"], OnPlane(map, bottomPlane)), curves[0], 0, pathPlane.Normal, bottomPlane));
        var tp = doc.Application.Create.NewCurveLoopsProfile(ToProfileLocal(Profile(top["loops"], OnPlane(map, topPlane)), curves[0], 1, pathPlane.Normal, topPlane));
        var sp = SketchPlane.Create(doc, pathPlane);
        return doc.FamilyCreate.NewSweptBlend((bool)f["solid"], curves[0], sp, bp, tp);
    }

    // ---------- упрощения LOD200 ----------

    /// <summary>Короб по габаритам (футы): прямоугольник в плоскости Z = min, выдавливание вверх.</summary>
    public static Element BoxFt(Document doc, XYZ min, XYZ max, bool solid)
    {
        var dz = Math.Max(max.Z - min.Z, 1 / Mm);
        double x0 = min.X, y0 = min.Y, x1 = Math.Max(max.X, min.X + 1 / Mm), y1 = Math.Max(max.Y, min.Y + 1 / Mm);
        var z = min.Z;
        var pts = new[] { new XYZ(x0, y0, z), new XYZ(x1, y0, z), new XYZ(x1, y1, z), new XYZ(x0, y1, z) };
        var ca = new CurveArray();
        for (var i = 0; i < 4; i++) ca.Append(Line.CreateBound(pts[i], pts[(i + 1) % 4]));
        var arr = new CurveArrArray();
        arr.Append(ca);
        var sp = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, z)));
        return doc.FamilyCreate.NewExtrusion(solid, arr, sp, dz);
    }

    public static Element Cylinder(Document doc, double diameterMm, double heightMm)
    {
        var r = diameterMm / 2 / Mm;
        var ca = new CurveArray();
        ca.Append(Arc.Create(XYZ.Zero, r, 0, Math.PI, XYZ.BasisX, XYZ.BasisY));
        ca.Append(Arc.Create(XYZ.Zero, r, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY));
        var arr = new CurveArrArray();
        arr.Append(ca);
        var sp = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));
        return doc.FamilyCreate.NewExtrusion(true, arr, sp, heightMm / Mm);
    }

    /// <summary>Габарит формы из снимка после преобразования (футы).</summary>
    public static void MappedBox(JObject f, Func<XYZ, XYZ> map, out XYZ min, out XYZ max)
    {
        var a = P(f["bbMin"]);
        var b = P(f["bbMax"]);
        var pts = new List<XYZ>();
        foreach (var x in new[] { a.X, b.X })
            foreach (var y in new[] { a.Y, b.Y })
                foreach (var z in new[] { a.Z, b.Z })
                    pts.Add(Ft(map(new XYZ(x, y, z))));
        min = new XYZ(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Min(p => p.Z));
        max = new XYZ(pts.Max(p => p.X), pts.Max(p => p.Y), pts.Max(p => p.Z));
    }

    /// <summary>Сдвиг без пути-эскиза или не созданный Revit: прямоугольные брусья по сегментам пути с сечением профиля.</summary>
    public static int SegmentBoxes(Document doc, JObject f, Func<XYZ, XYZ> map)
    {
        var path = f["path"] as JObject;
        var profileSk = (f["profile"] ?? f["bottom"]) as JObject;
        if (path == null || profileSk == null) return 0;
        XYZ w;
        var pathPlane = MapPlane((JObject)path["plane"], map, 0, out w);
        var nn = pathPlane.Normal;
        var curves = PathCurves(path, map);
        if (curves.Count == 0) return 0;
        var profPlane = MapPlane((JObject)profileSk["plane"], map, 0, out w);
        var profPts = new List<XYZ>();
        foreach (JArray loop in profileSk["loops"])
            foreach (JObject c in loop)
                foreach (var cv in Curves(c, map)) profPts.AddRange(cv.Tessellate());
        if (profPts.Count == 0) return 0;
        var pc = new XYZ(profPts.Average(p => p.X), profPts.Average(p => p.Y), profPts.Average(p => p.Z));
        int index;
        ProfilePlaneLocation location;
        FindProfileLocation(curves, profPlane, out index, out location);
        var prm = location == ProfilePlaneLocation.Start ? 0.0 : location == ProfilePlaneLocation.MidPoint ? 0.5 : 1.0;
        var lp = curves[index].Evaluate(prm, true);
        var tl = curves[index].ComputeDerivatives(prm, true).BasisX.Normalize();
        var q0 = nn.CrossProduct(tl).Normalize();
        var off = pc - lp;
        double offN = off.DotProduct(nn), offQ = off.DotProduct(q0);
        double h = Math.Max(profPts.Max(p => (p - pc).DotProduct(nn)) - profPts.Min(p => (p - pc).DotProduct(nn)), 2 / Mm);
        double wq = Math.Max(profPts.Max(p => (p - pc).DotProduct(q0)) - profPts.Min(p => (p - pc).DotProduct(q0)), 2 / Mm);
        XYZ fmin = null, fmax = null;
        if (f["bbMin"] != null)
        {
            MappedBox(f, map, out fmin, out fmax);
            var tol = 10 / Mm;
            fmin = fmin - new XYZ(tol, tol, tol);
            fmax = fmax + new XYZ(tol, tol, tol);
        }
        var made = 0;
        foreach (var c in curves)
        {
            var pts = c is Line ? new List<XYZ> { c.GetEndPoint(0), c.GetEndPoint(1) } : c.Tessellate().ToList();
            for (var i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i];
                var b = pts[i + 1];
                var len = a.DistanceTo(b);
                if (len < 1 / Mm) continue;
                var t = (b - a).Normalize();
                var q = nn.CrossProduct(t).Normalize();
                var o = a + nn * offN + q * offQ;
                var corners = new[] { o + q * (wq / 2) + nn * (h / 2), o - q * (wq / 2) + nn * (h / 2), o - q * (wq / 2) - nn * (h / 2), o + q * (wq / 2) - nn * (h / 2) };
                if (fmin != null)
                    foreach (var p in corners.Concat(corners.Select(x => x + t * len)))
                        if (p.X < fmin.X || p.Y < fmin.Y || p.Z < fmin.Z || p.X > fmax.X || p.Y > fmax.Y || p.Z > fmax.Z)
                            throw new Exception("брусья выходят за габарит формы");
                var ca = new CurveArray();
                for (var k = 0; k < 4; k++) ca.Append(Line.CreateBound(corners[k], corners[(k + 1) % 4]));
                var arr = new CurveArrArray();
                arr.Append(ca);
                var sp = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(t, o));
                doc.FamilyCreate.NewExtrusion((bool)f["solid"], arr, sp, len);
                made++;
            }
        }
        return made;
    }

    // ---------- обход снимка ----------

    public static bool IsTemplateHost(JObject f)
    {
        if (f["bbMin"] == null) return false;
        var a = P(f["bbMin"]);
        var b = P(f["bbMax"]);
        return (string)f["kind"] == "Extrusion" && Math.Abs(b.X - a.X - 2438.4) < 0.2 && Math.Abs(b.Y - a.Y - 2438.4) < 0.2 && Math.Abs(b.Z - a.Z - 304.8) < 0.2;
    }

    static void LocalBox(JObject dump, out XYZ min, out XYZ max)
    {
        var boxes = new List<XYZ[]>();
        foreach (JObject f in dump["forms"])
            if ((bool)f["solid"] && f["bbMin"] != null && !IsTemplateHost(f)) boxes.Add(new[] { P(f["bbMin"]), P(f["bbMax"]) });
        foreach (JObject i in dump["nestedInstances"])
            if (i["bbMin"] != null) boxes.Add(new[] { P(i["bbMin"]), P(i["bbMax"]) });
        if (boxes.Count == 0) { min = max = null; return; }
        min = new XYZ(boxes.Min(b => b[0].X), boxes.Min(b => b[0].Y), boxes.Min(b => b[0].Z));
        max = new XYZ(boxes.Max(b => b[1].X), boxes.Max(b => b[1].Y), boxes.Max(b => b[1].Z));
    }

    /// <summary>Формы снимка с цепочкой преобразований: вложенные семейства — в координаты родителя и в габарит своего экземпляра.</summary>
    public static void CollectForms(JObject dump, Func<XYZ, XYZ> map, List<Tuple<JObject, Func<XYZ, XYZ>>> output, int depth)
    {
        foreach (JObject f in dump["forms"])
            if (!IsTemplateHost(f)) output.Add(Tuple.Create(f, map));
        if (depth > 2) return;
        var nestedByName = new Dictionary<string, JObject>();
        foreach (JObject n in dump["nestedFamilies"])
            if (n["familyName"] != null && n["forms"] != null) nestedByName[(string)n["familyName"]] = n;
        foreach (JObject inst in dump["nestedInstances"])
        {
            JObject nd;
            if (!nestedByName.TryGetValue((string)inst["family"], out nd)) continue;
            var o = P(inst["origin"]);
            var bx = P(inst["basisX"]).Normalize();
            var bz = P(inst["basisZ"]).Normalize();
            var by = bz.CrossProduct(bx);
            Func<XYZ, XYZ> place = p => o + bx * p.X + by * p.Y + bz * p.Z;
            XYZ lmin, lmax;
            LocalBox(nd, out lmin, out lmax);
            Func<XYZ, XYZ> fit = p => p;
            if (lmin != null && inst["bbMin"] != null)
            {
                var pts = new List<XYZ>();
                foreach (var x in new[] { lmin.X, lmax.X })
                    foreach (var y in new[] { lmin.Y, lmax.Y })
                        foreach (var z in new[] { lmin.Z, lmax.Z })
                            pts.Add(place(new XYZ(x, y, z)));
                var tmin = new XYZ(pts.Min(p => p.X), pts.Min(p => p.Y), pts.Min(p => p.Z));
                var tmax = new XYZ(pts.Max(p => p.X), pts.Max(p => p.Y), pts.Max(p => p.Z));
                var imin = P(inst["bbMin"]);
                var imax = P(inst["bbMax"]);
                Func<double, double, double> k = (want, have) => have > 0.5 ? want / have : 1.0;
                var kx = k(imax.X - imin.X, tmax.X - tmin.X);
                var ky = k(imax.Y - imin.Y, tmax.Y - tmin.Y);
                var kz = k(imax.Z - imin.Z, tmax.Z - tmin.Z);
                fit = p => new XYZ(imin.X + (p.X - tmin.X) * kx, imin.Y + (p.Y - tmin.Y) * ky, imin.Z + (p.Z - tmin.Z) * kz);
            }
            var outer = map;
            Func<XYZ, XYZ> chain = p => outer(fit(place(p)));
            CollectForms(nd, chain, output, depth + 1);
        }
    }

    // ---------- семейство ----------

    /// <summary>Точный габарит твёрдой геометрии (футы) по разбивке рёбер; плита-основа шаблона не входит.</summary>
    public static bool MeasureFt(Document doc, out XYZ min, out XYZ max)
    {
        min = max = null;
        var options = new Options { DetailLevel = ViewDetailLevel.Fine };
        var elements = new FilteredElementCollector(doc).OfClass(typeof(GenericForm)).Cast<GenericForm>().Where(x => x.IsSolid).Cast<Element>()
            .Concat(new FilteredElementCollector(doc).OfClass(typeof(GeomCombination))).ToList();
        foreach (var f in elements)
        {
            var bb = f.get_BoundingBox(null);
            if (bb != null && f is Extrusion)
            {
                var sz = (bb.Max - bb.Min) * Mm;
                if (Math.Abs(sz.X - 2438.4) < 0.2 && Math.Abs(sz.Y - 2438.4) < 0.2 && Math.Abs(sz.Z - 304.8) < 0.2) continue;
            }
            foreach (var obj in f.get_Geometry(options))
            {
                var solid = obj as Solid;
                if (solid == null || solid.Volume <= 0) continue;
                foreach (Edge edge in solid.Edges)
                    foreach (var p in EdgePoints(edge))
                    {
                        min = min == null ? p : new XYZ(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                        max = max == null ? p : new XYZ(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
                    }
            }
        }
        return min != null;
    }

    public static IEnumerable<XYZ> EdgePoints(Edge edge)
    {
        var curve = edge.AsCurve();
        if (curve is Line) return new[] { curve.GetEndPoint(0), curve.GetEndPoint(1) };
        return Enumerable.Range(0, 181).Select(i => curve.Evaluate(i / 180.0, true));
    }

    static Document NewDoc(Autodesk.Revit.ApplicationServices.Application app, JObject job, JArray log)
    {
        var doc = app.NewFamilyDocument((string)job["template"]);
        if (doc == null) throw new Exception("шаблон не открылся: " + job["template"]);
        string err;
        var bic = (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), (string)job["category"]);
        if (!Run(doc, "Категория", () =>
        {
            var cat = Category.GetCategory(doc, bic);
            if (doc.OwnerFamily.FamilyCategory.Id != cat.Id) doc.OwnerFamily.FamilyCategory = cat;
        }, out err)) log.Add("категория: " + err);
        return doc;
    }

    /// <summary>Формы исходника в документ по преобразованию (поворот на 90°, центр c, масштаб s; мм).</summary>
    static JObject RebuildGeometry(Document doc, JObject dump, bool rotate, XYZ c, XYZ s, double[] size, JArray log)
    {
        string err;
        int ok = 0, fallback = 0, failed = 0;
        Func<XYZ, XYZ> global = p =>
        {
            var q = rotate ? new XYZ(-p.Y, p.X, p.Z) : p;
            return new XYZ((q.X - c.X) * s.X, (q.Y - c.Y) * s.Y, (q.Z - c.Z) * s.Z);
        };
        var forms = new List<Tuple<JObject, Func<XYZ, XYZ>>>();
        CollectForms(dump, global, forms, 0);
        var voids = new List<ElementId>();
        var solids = new List<ElementId>();
        var stats = new JObject();
        foreach (var item in forms)
        {
            var f = item.Item1;
            var map = item.Item2;
            var formKind = (string)f["kind"];
            var isSolid = (bool)f["solid"];
            Element created = null;
            var done = Run(doc, formKind, () =>
            {
                switch (formKind)
                {
                    case "Extrusion": created = Extrusion(doc, f, map); break;
                    case "Revolution": created = Revolution(doc, f, map); break;
                    case "Blend": created = Blend(doc, f, map); break;
                    case "Sweep": created = Sweep(doc, f, map); break;
                    case "SweptBlend": created = SweptBlend(doc, f, map); break;
                    default: throw new Exception("неизвестная форма " + formKind);
                }
            }, out err);
            if (done)
            {
                ok++;
                (isSolid ? solids : voids).Add(created.Id);
                continue;
            }
            var firstError = err;
            // Упрощение LOD200: сдвиг — брусьями по пути; остальное — коробом по габариту формы.
            var segments = 0;
            if ((formKind == "Sweep" || formKind == "SweptBlend") &&
                Run(doc, "Брусья по пути", () => { segments = SegmentBoxes(doc, f, map); if (segments == 0) throw new Exception("нет сегментов"); }, out err))
            {
                fallback++;
                log.Add(formKind + " " + f["id"] + ": " + firstError + " → брусья по пути (" + segments + ")");
                continue;
            }
            if (isSolid && f["bbMin"] != null && Run(doc, "Короб по габариту", () =>
            {
                XYZ a, b;
                MappedBox(f, map, out a, out b);
                created = BoxFt(doc, a, b, true);
            }, out err))
            {
                fallback++;
                solids.Add(created.Id);
                log.Add(formKind + " " + f["id"] + ": " + firstError + " → короб по габариту");
                continue;
            }
            failed++;
            log.Add(formKind + " " + f["id"] + (isSolid ? "" : " (пустотная)") + ": " + firstError + " → пропущена");
        }
        // Пустотные формы режут твёрдые, которые пересекают.
        if (voids.Count > 0 && solids.Count > 0)
        {
            var cuts = 0;
            // В семействе пустотная форма режет твёрдые через объединение геометрии (как «Объединить» в редакторе).
            foreach (var v in voids)
            {
                var vbb = doc.GetElement(v).get_BoundingBox(null);
                var targets = solids.Where(sId =>
                {
                    var b = doc.GetElement(sId).get_BoundingBox(null);
                    return vbb != null && b != null && b.Min.X <= vbb.Max.X && b.Max.X >= vbb.Min.X && b.Min.Y <= vbb.Max.Y &&
                           b.Max.Y >= vbb.Min.Y && b.Min.Z <= vbb.Max.Z && b.Max.Z >= vbb.Min.Z;
                }).ToList();
                foreach (var sId in targets)
                {
                    if (Run(doc, "Вырезание", () =>
                    {
                        var arr = new CombinableElementArray();
                        arr.Append((CombinableElement)doc.GetElement(sId));
                        arr.Append((CombinableElement)doc.GetElement(v));
                        doc.CombineElements(arr);
                    }, out err)) cuts++;
                }
            }
            stats["voidCuts"] = cuts;
        }
        if (solids.Count == 0 && Run(doc, "Короб по заданию",
                () => BoxFt(doc, Ft(new XYZ(-size[0] / 2, -size[1] / 2, 0)), Ft(new XYZ(size[0] / 2, size[1] / 2, size[2])), true), out err))
        {
            fallback++;
            log.Add("ни одной формы исходника не построено → короб по габариту задания");
        }
        stats["ok"] = ok;
        stats["simplified"] = fallback;
        stats["skipped"] = failed;
        return stats;
    }

    public static JObject Build(Document anyDoc, JObject job, string fop)
    {
        var app = anyDoc.Application;
        var res = new JObject { ["family"] = job["family"], ["ok"] = false };
        var output = (string)job["output"];
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var log = new JArray();
        var doc = NewDoc(app, job, log);
        try
        {
            string err;
            var kind = (string)job["kind"];
            var size = job["size"].Select(x => (double)x).ToArray();
            if (kind == "box")
            {
                var done = Run(doc, "Кубик", () => BoxFt(doc, Ft(new XYZ(-size[0] / 2, -size[1] / 2, 0)), Ft(new XYZ(size[0] / 2, size[1] / 2, size[2])), true), out err);
                if (!done) log.Add("кубик: " + err);
                res["forms"] = new JObject { ["ok"] = done ? 1 : 0, ["simplified"] = 0, ["skipped"] = done ? 0 : 1 };
            }
            else if (kind == "cylinder")
            {
                var done = Run(doc, "Цилиндр", () => Cylinder(doc, size[0], size[2]), out err);
                if (!done) log.Add("цилиндр: " + err);
                res["forms"] = new JObject { ["ok"] = done ? 1 : 0, ["simplified"] = 0, ["skipped"] = done ? 0 : 1 };
            }
            else
            {
                var dump = JObject.Parse(File.ReadAllText((string)job["sourceJson"]));
                var m = (JObject)job["map"];
                var rotate = (bool)m["rotate"];
                var c = P(m["center"]);
                var s = P(m["scale"]);
                var face = (bool)job["faceBased"];
                var firstLog = new JArray();
                // Габариты элементов исходника неточные: строим, измеряем и пересобираем с поправкой (до 3 проходов, берём лучший).
                var bestDev = double.MaxValue;
                XYZ bestC = c, bestS = s;
                var passes = new JArray();
                JObject stats = null;
                var current = -1;
                for (var pass = 0; pass < 3; pass++)
                {
                    if (pass > 0)
                    {
                        doc.Close(false);
                        doc = NewDoc(app, job, new JArray());
                    }
                    var passLog = new JArray();
                    stats = RebuildGeometry(doc, dump, rotate, c, s, size, passLog);
                    current = pass;
                    XYZ mn, mx;
                    if (!MeasureFt(doc, out mn, out mx)) { firstLog = passLog; break; }
                    mn = mn * Mm;
                    mx = mx * Mm;
                    var got = mx - mn;
                    var cx = (mn.X + mx.X) / 2;
                    var cy = (mn.Y + mx.Y) / 2;
                    var dev = Math.Max(Math.Max(Math.Abs(got.X - size[0]), Math.Abs(got.Y - size[1])), Math.Abs(got.Z - size[2]));
                    dev = Math.Max(dev, Math.Max(Math.Abs(cx), Math.Max(face ? 0 : Math.Abs(cy), Math.Abs(mn.Z))));
                    passes.Add(new JObject
                    {
                        ["size"] = new JArray(Math.Round(got.X, 1), Math.Round(got.Y, 1), Math.Round(got.Z, 1)),
                        ["center"] = new JArray(Math.Round(cx, 1), Math.Round(cy, 1), Math.Round(mn.Z, 1)),
                        ["deviationMm"] = Math.Round(dev, 2)
                    });
                    if (dev < bestDev) { bestDev = dev; bestC = c; bestS = s; firstLog = passLog; }
                    if (dev <= 0.5) break;
                    // Габарит источника в повёрнутых осях: a = (src − c)·s  →  src = a/s + c.
                    var cNext = new XYZ(cx / s.X + c.X, face ? c.Y : cy / s.Y + c.Y, mn.Z / s.Z + c.Z);
                    s = new XYZ(s.X * size[0] / got.X, s.Y * size[1] / got.Y, s.Z * size[2] / got.Z);
                    c = cNext;
                }
                var lastIsBest = passes.Count == 0 || Math.Abs((double)passes[passes.Count - 1]["deviationMm"] - bestDev) < 1e-9;
                if (!lastIsBest)
                {
                    doc.Close(false);
                    doc = NewDoc(app, job, new JArray());
                    firstLog = new JArray();
                    stats = RebuildGeometry(doc, dump, rotate, bestC, bestS, size, firstLog);
                }
                foreach (var x in firstLog) log.Add(x);
                res["passes"] = passes;
                res["forms"] = stats;
            }

            if (!AddAdsk(doc, app, fop, job, out err)) throw new Exception("параметры ADSK: " + err);

            if (File.Exists(output)) File.Delete(output);
            doc.SaveAs(output, new SaveAsOptions { OverwriteExistingFile = true, MaximumBackups = 1 });
            res["ok"] = true;
            res["output"] = output;
        }
        finally
        {
            doc.Close(false);
            res["log"] = log;
        }
        return res;
    }

    public static bool AddAdsk(Document doc, Autodesk.Revit.ApplicationServices.Application app, string fop, JObject job, out string error)
    {
        var previous = app.SharedParametersFilename;
        app.SharedParametersFilename = fop;
        try
        {
            var file = app.OpenSharedParameterFile();
            if (file == null) { error = "ФОП не открылся: " + fop; return false; }
            var defs = new Dictionary<string, ExternalDefinition>();
            foreach (DefinitionGroup g in file.Groups)
                foreach (Definition d in g.Definitions)
                    if (d is ExternalDefinition && !defs.ContainsKey(d.Name)) defs[d.Name] = (ExternalDefinition)d;
            var family = (string)job["family"];
            var specs = new[]
            {
                new object[] { "ADSK_Наименование", false, (string)job["adskName"] },
                new object[] { "ADSK_Марка", false, (string)job["adskMark"] },
                new object[] { "ADSK_Позиция", false, (string)job["position"] },
            };
            return Run(doc, "Параметры ADSK", () =>
            {
                var fm = doc.FamilyManager;
                if (fm.Types.Size == 0) fm.NewType(family);
                else if (fm.CurrentType.Name != family) fm.RenameCurrentType(family);
                foreach (var spec in specs)
                {
                    var name = (string)spec[0];
                    var fp = fm.get_Parameter(name);
                    if (fp == null)
                    {
                        ExternalDefinition def;
                        if (!defs.TryGetValue(name, out def)) throw new Exception("нет в ФОП: " + name);
                        fp = fm.AddParameter(def, BuiltInParameterGroup.PG_DATA, (bool)spec[1]);
                    }
                    fm.Set(fp, (string)spec[2]);
                }
            }, out error);
        }
        finally
        {
            try { app.SharedParametersFilename = previous ?? ""; } catch { }
        }
    }
}
