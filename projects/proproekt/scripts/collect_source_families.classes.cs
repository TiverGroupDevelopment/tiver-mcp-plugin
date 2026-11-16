// Снимок исходного семейства для пересборки в Revit 2019: параметры, типоразмеры, формы с эскизами,
// опорные плоскости, размеры с метками, вложенные семейства. Единицы — мм, углы — градусы.
public static class SourceFamilyDump
{
    const double Mm = 304.8;

    public static double R(double feet) { return Math.Round(feet * Mm, 2); }

    public static JArray P(XYZ p) { return new JArray(R(p.X), R(p.Y), R(p.Z)); }

    public static JArray V(XYZ p) { return new JArray(Math.Round(p.X, 5), Math.Round(p.Y, 5), Math.Round(p.Z, 5)); }

    public static JObject CurveJson(Curve c)
    {
        var o = new JObject();
        var line = c as Line;
        var arc = c as Arc;
        var ellipse = c as Ellipse;
        if (line != null)
        {
            o["t"] = "line"; o["p0"] = P(line.GetEndPoint(0)); o["p1"] = P(line.GetEndPoint(1));
        }
        else if (arc != null)
        {
            o["t"] = "arc"; o["center"] = P(arc.Center); o["r"] = R(arc.Radius); o["normal"] = V(arc.Normal);
            if (arc.IsBound) { o["p0"] = P(arc.GetEndPoint(0)); o["p1"] = P(arc.GetEndPoint(1)); o["mid"] = P(arc.Evaluate(0.5, true)); }
            else { o["closed"] = true; o["xdir"] = V(arc.XDirection); }
        }
        else if (ellipse != null)
        {
            o["t"] = "ellipse"; o["center"] = P(ellipse.Center); o["rx"] = R(ellipse.RadiusX); o["ry"] = R(ellipse.RadiusY);
            o["xdir"] = V(ellipse.XDirection); o["ydir"] = V(ellipse.YDirection);
            if (ellipse.IsBound) { o["p0"] = P(ellipse.GetEndPoint(0)); o["p1"] = P(ellipse.GetEndPoint(1)); o["mid"] = P(ellipse.Evaluate(0.5, true)); }
            else o["closed"] = true;
        }
        else
        {
            o["t"] = c.GetType().Name;
            o["pts"] = new JArray(c.Tessellate().Select(x => (object)P(x)).ToArray());
        }
        return o;
    }

    public static JArray Loops(CurveArrArray profile)
    {
        var result = new JArray();
        if (profile == null) return result;
        foreach (CurveArray loop in profile)
        {
            var items = new JArray();
            foreach (Curve c in loop) items.Add(CurveJson(c));
            result.Add(items);
        }
        return result;
    }

    public static JObject PlaneJson(SketchPlane sp)
    {
        if (sp == null) return null;
        var pl = sp.GetPlane();
        return new JObject { ["name"] = sp.Name, ["origin"] = P(pl.Origin), ["normal"] = V(pl.Normal), ["xvec"] = V(pl.XVec) };
    }

    public static JObject SketchJson(Sketch s)
    {
        if (s == null) return null;
        return new JObject { ["plane"] = PlaneJson(s.SketchPlane), ["loops"] = Loops(s.Profile) };
    }

    public static string Id(Element e) { return e.Id.ToString(); }

    public static JObject FormJson(Document d, GenericForm f)
    {
        var o = new JObject { ["id"] = Id(f), ["kind"] = f.GetType().Name, ["solid"] = f.IsSolid };
        try
        {
            var vis = f.GetVisibility();
            o["visibility"] = new JObject
            {
                ["coarse"] = vis.IsShownInCoarse, ["medium"] = vis.IsShownInMedium, ["fine"] = vis.IsShownInFine,
                ["planRCP"] = vis.IsShownInTopBottom, ["frontBack"] = vis.IsShownInFrontBack, ["leftRight"] = vis.IsShownInLeftRight
            };
        }
        catch { }
        if (f.Subcategory != null) o["subcategory"] = f.Subcategory.Name;
        var mp = f.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
        if (mp != null)
        {
            var m = d.GetElement(mp.AsElementId()) as Material;
            if (m != null) o["material"] = m.Name;
        }
        var assoc = new JObject();
        foreach (Parameter p in f.Parameters)
        {
            try
            {
                var fp = d.FamilyManager.GetAssociatedFamilyParameter(p);
                if (fp != null) assoc[p.Definition.Name] = fp.Definition.Name;
            }
            catch { }
        }
        if (assoc.Count > 0) o["associated"] = assoc;
        var bb = f.get_BoundingBox(null);
        if (bb != null) { o["bbMin"] = P(bb.Min); o["bbMax"] = P(bb.Max); }
        try
        {
            var solids = f.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine }).OfType<Solid>().Where(s => s.Volume > 0).ToList();
            o["volumeL"] = Math.Round(solids.Sum(s => s.Volume) * 28.316846592, 3);
            o["faces"] = solids.Sum(s => s.Faces.Size);
        }
        catch { }

        try
        {
            if (f is Extrusion)
            {
                var e = (Extrusion)f;
                o["sketch"] = SketchJson(e.Sketch); o["start"] = R(e.StartOffset); o["end"] = R(e.EndOffset);
            }
            else if (f is Revolution)
            {
                var r = (Revolution)f;
                o["sketch"] = SketchJson(r.Sketch);
                if (r.Axis != null) o["axis"] = CurveJson(r.Axis.GeometryCurve);
                o["startAngle"] = Math.Round(r.StartAngle * 180 / Math.PI, 3); o["endAngle"] = Math.Round(r.EndAngle * 180 / Math.PI, 3);
            }
            else if (f is SweptBlend)
            {
                var sb = (SweptBlend)f;
                o["path"] = SketchJson(sb.PathSketch);
                o["bottom"] = SketchJson(sb.BottomSketch); o["top"] = SketchJson(sb.TopSketch);
                if (sb.BottomProfileSymbol != null) o["bottomProfileFamily"] = sb.BottomProfileSymbol.Profile.FamilyName + " : " + sb.BottomProfileSymbol.Profile.Name;
                if (sb.TopProfileSymbol != null) o["topProfileFamily"] = sb.TopProfileSymbol.Profile.FamilyName + " : " + sb.TopProfileSymbol.Profile.Name;
            }
            else if (f is Sweep)
            {
                var s = (Sweep)f;
                o["path"] = SketchJson(s.PathSketch);
                if (s.PathSketch == null) o["path3d"] = true;
                o["profile"] = SketchJson(s.ProfileSketch);
                if (s.ProfileSymbol != null) o["profileFamily"] = s.ProfileSymbol.Profile.FamilyName + " : " + s.ProfileSymbol.Profile.Name;
            }
            else if (f is Blend)
            {
                var b = (Blend)f;
                o["bottom"] = SketchJson(b.BottomSketch); o["top"] = SketchJson(b.TopSketch);
                o["bottomOffset"] = R(b.BottomOffset); o["topOffset"] = R(b.TopOffset);
            }
        }
        catch (Exception ex) { o["recipeError"] = ex.Message; }
        return o;
    }

    public static string ValueString(FamilyType t, FamilyParameter fp)
    {
        try
        {
            if (!t.HasValue(fp)) return null;
            switch (fp.StorageType)
            {
                case StorageType.String: return t.AsString(fp);
                case StorageType.ElementId:
                    var id = t.AsElementId(fp);
                    return id == null ? null : id.ToString();
                default:
                    return t.AsValueString(fp);
            }
        }
        catch { return null; }
    }

    public static JObject Dump(Document d, int depth)
    {
        var fam = d.OwnerFamily;
        var fm = d.FamilyManager;
        var o = new JObject();
        o["category"] = fam.FamilyCategory != null ? fam.FamilyCategory.Name : null;
        try { o["builtInCategory"] = fam.FamilyCategory.BuiltInCategory.ToString(); } catch { }
        try { o["placement"] = fam.FamilyPlacementType.ToString(); } catch { }
        Func<BuiltInParameter, string> fparam = bip =>
        {
            var p = fam.get_Parameter(bip);
            return p == null ? null : (p.AsValueString() ?? p.AsInteger().ToString());
        };
        o["workPlaneBased"] = fparam(BuiltInParameter.FAMILY_WORK_PLANE_BASED);
        o["alwaysVertical"] = fparam(BuiltInParameter.FAMILY_ALWAYS_VERTICAL);
        o["shared"] = fparam(BuiltInParameter.FAMILY_SHARED);
        o["hosting"] = fparam(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR);

        var types = new JArray();
        foreach (FamilyType t in fm.Types) types.Add(t.Name);
        o["types"] = types;

        var pars = new JArray();
        foreach (FamilyParameter fp in fm.Parameters)
        {
            var po = new JObject
            {
                ["name"] = fp.Definition.Name,
                ["instance"] = fp.IsInstance,
                ["shared"] = fp.IsShared,
                ["storage"] = fp.StorageType.ToString()
            };
            try { po["dataType"] = fp.Definition.GetDataType().TypeId; } catch { }
            try { po["group"] = fp.Definition.GetGroupTypeId().TypeId; } catch { }
            if (fp.IsShared) { try { po["guid"] = fp.GUID.ToString(); } catch { } }
            if (!string.IsNullOrEmpty(fp.Formula)) po["formula"] = fp.Formula;
            if (fp.IsReporting) po["reporting"] = true;
            var values = new JObject();
            foreach (FamilyType t in fm.Types)
            {
                var v = ValueString(t, fp);
                if (!string.IsNullOrEmpty(v)) values[t.Name] = v;
            }
            if (values.Count > 0) po["values"] = values;
            pars.Add(po);
        }
        o["parameters"] = pars;

        var forms = new FilteredElementCollector(d).OfClass(typeof(GenericForm)).Cast<GenericForm>().ToList();
        o["forms"] = new JArray(forms.Select(f => (object)FormJson(d, f)).ToArray());
        o["formSummary"] = string.Join(", ", forms.GroupBy(f => f.GetType().Name + (f.IsSolid ? "" : "(void)")).Select(g => g.Key + " " + g.Count()));

        var planes = new JArray();
        foreach (var rp in new FilteredElementCollector(d).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>())
        {
            var definesOrigin = rp.get_Parameter(BuiltInParameter.DATUM_PLANE_DEFINES_ORIGIN);
            planes.Add(new JObject
            {
                ["name"] = rp.Name, ["bubble"] = P(rp.BubbleEnd), ["free"] = P(rp.FreeEnd), ["normal"] = V(rp.Normal),
                ["definesOrigin"] = definesOrigin != null && definesOrigin.AsInteger() == 1
            });
        }
        o["referencePlanes"] = planes;

        var dims = new JArray();
        foreach (var dim in new FilteredElementCollector(d).OfClass(typeof(Dimension)).Cast<Dimension>())
        {
            FamilyParameter label = null;
            try { label = dim.FamilyLabel; } catch { }
            if (label == null) continue;
            dims.Add(new JObject
            {
                ["label"] = label.Definition.Name,
                ["value"] = dim.Value.HasValue ? (JToken)R(dim.Value.Value) : null,
                ["references"] = dim.References.Size,
                ["view"] = d.GetElement(dim.OwnerViewId) != null ? d.GetElement(dim.OwnerViewId).Name : null
            });
        }
        o["labeledDimensions"] = dims;

        var other = new JObject();
        other["imports"] = new JArray(new FilteredElementCollector(d).OfClass(typeof(ImportInstance)).Cast<ImportInstance>()
            .Select(i => (object)(i.Category != null ? i.Category.Name : "import")).ToArray());
        other["freeForms"] = new FilteredElementCollector(d).OfClass(typeof(FreeFormElement)).GetElementCount();
        other["directShapes"] = new FilteredElementCollector(d).OfClass(typeof(DirectShape)).GetElementCount();
        var curves = new FilteredElementCollector(d).OfClass(typeof(CurveElement)).Cast<CurveElement>().ToList();
        other["modelLines"] = curves.Count(c => c.CurveElementType == CurveElementType.ModelCurve);
        other["symbolicLines"] = curves.Count(c => c.CurveElementType == CurveElementType.SymbolicCurve);
        other["modelText"] = new FilteredElementCollector(d).OfClass(typeof(ModelText)).GetElementCount();
        other["groups"] = new FilteredElementCollector(d).OfClass(typeof(Group)).GetElementCount();
        other["connectors"] = new FilteredElementCollector(d).OfClass(typeof(ConnectorElement)).GetElementCount();
        other["subcategories"] = new JArray(fam.FamilyCategory == null ? new object[0]
            : fam.FamilyCategory.SubCategories.Cast<Category>().Select(c => (object)c.Name).ToArray());
        o["other"] = other;

        // Общий габарит: формы и вложенные экземпляры.
        BoundingBoxXYZ total = null;
        Action<BoundingBoxXYZ> grow = bb =>
        {
            if (bb == null) return;
            if (total == null) { total = new BoundingBoxXYZ { Min = bb.Min, Max = bb.Max }; return; }
            total.Min = new XYZ(Math.Min(total.Min.X, bb.Min.X), Math.Min(total.Min.Y, bb.Min.Y), Math.Min(total.Min.Z, bb.Min.Z));
            total.Max = new XYZ(Math.Max(total.Max.X, bb.Max.X), Math.Max(total.Max.Y, bb.Max.Y), Math.Max(total.Max.Z, bb.Max.Z));
        };
        foreach (var f in forms) if (f.IsSolid) grow(f.get_BoundingBox(null));

        var instances = new JArray();
        foreach (var fi in new FilteredElementCollector(d).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
        {
            grow(fi.get_BoundingBox(null));
            var tr = fi.GetTransform();
            var io = new JObject
            {
                ["family"] = fi.Symbol.Family.Name, ["type"] = fi.Symbol.Name,
                ["category"] = fi.Category != null ? fi.Category.Name : null,
                ["origin"] = P(tr.Origin), ["basisX"] = V(tr.BasisX), ["basisZ"] = V(tr.BasisZ)
            };
            var ibb = fi.get_BoundingBox(null);
            if (ibb != null) { io["bbMin"] = P(ibb.Min); io["bbMax"] = P(ibb.Max); }
            instances.Add(io);
        }
        o["nestedInstances"] = instances;
        if (total != null)
        {
            o["bbMin"] = P(total.Min); o["bbMax"] = P(total.Max);
            var size = total.Max - total.Min;
            o["sizeXYZ"] = new JArray(R(size.X), R(size.Y), R(size.Z));
        }

        var nested = new JArray();
        if (depth < 2)
        {
            foreach (var nf in new FilteredElementCollector(d).OfClass(typeof(Family)).Cast<Family>()
                         .Where(x => x.FamilyCategory != null && x.FamilyCategory.CategoryType == CategoryType.Model && x.IsEditable))
            {
                Document nd = null;
                try
                {
                    nd = d.EditFamily(nf);
                    var no = Dump(nd, depth + 1);
                    no["familyName"] = nf.Name;
                    nested.Add(no);
                }
                catch (Exception ex) { nested.Add(new JObject { ["familyName"] = nf.Name, ["error"] = ex.Message }); }
                finally { if (nd != null) nd.Close(false); }
            }
        }
        o["nestedFamilies"] = nested;
        return o;
    }

    public static string SafeName(string name)
    {
        var bad = System.IO.Path.GetInvalidFileNameChars();
        return new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
    }
}
