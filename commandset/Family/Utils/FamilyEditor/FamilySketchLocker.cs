using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

/// <summary>
/// Закрепляет линии эскиза форм (выдавливание, вращение) и ось вращения на опорных плоскостях:
/// прямая линия эскиза, которая лежит в плоскости, выравнивается с ней с замком (NewAlignment),
/// и форма двигается вместе с плоскостью, когда параметр двигает плоскость.
/// align_family_elements закрепляет только плоские грани; эскиз тела вращения так не закрепить.
///
/// Класс самодостаточный (только Revit API) и написан в стиле C# 5 без ElementId.IntegerValue:
/// этот же файл подключается в send_code_to_revit (classes), который в Revit 2019 компилирует CodeDom.
/// Транзакцию открывает вызывающий код.
/// </summary>
public static class FamilySketchLocker
{
    /// <summary>Допуск совпадения линии с плоскостью по умолчанию, футы (~0,003 мм).</summary>
    public const double DefaultToleranceFeet = 1e-5;

    /// <summary>
    /// forms[i] закрепляется только на плоскостях planeNames[i]; lockAxis[i] — закрепить и ось вращения
    /// (для Revolution: ось — отдельная линия эскиза; без замка ось остаётся на месте, когда профиль уезжает).
    /// </summary>
    public static object Lock(Document doc, IList<GenericForm> forms, IList<HashSet<string>> planeNames, IList<bool> lockAxis, double tolerance)
    {
        if (tolerance <= 0) tolerance = DefaultToleranceFeet;
        var planes = new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>().ToList();
        var done = new List<object>();
        var axes = new List<object>();
        var free = new List<object>();
        var skipped = new List<object>();
        for (int i = 0; i < forms.Count; i++)
        {
            var form = forms[i];
            var formId = form.Id.ToString();
            var names = planeNames[i];
            foreach (var name in names)
            {
                if (!planes.Any(p => p.Name == name)) skipped.Add(new { form = formId, plane = name, reason = "плоскости с таким именем нет" });
            }

            var sketch = SketchOf(form);
            if (sketch == null)
            {
                skipped.Add(new { form = formId, reason = "у формы нет эскиза (" + form.GetType().Name + ")" });
                continue;
            }

            var normal = sketch.SketchPlane.GetPlane().Normal;
            var view = FindView(doc, normal);
            if (view == null)
            {
                skipped.Add(new { form = formId, reason = "нет вида, в котором плоскость эскиза видна плашмя" });
                continue;
            }

            var candidates = planes.Where(p => names.Contains(p.Name)).ToList();
            foreach (CurveArray loop in sketch.Profile)
            {
                foreach (Curve curve in loop)
                {
                    var line = curve as Line;
                    if (line == null || curve.Reference == null) continue;
                    var locked = false;
                    foreach (var plane in candidates)
                    {
                        if (!LiesIn(line, plane, normal, tolerance)) continue;
                        string error;
                        if (TryAlign(doc, view, plane, curve.Reference, out error))
                        {
                            done.Add(new { form = formId, plane = plane.Name });
                            locked = true;
                        }
                        else
                        {
                            skipped.Add(new { form = formId, plane = plane.Name, error = error });
                        }
                    }

                    if (!locked) free.Add(new { form = formId, from = Mm(line.GetEndPoint(0)), to = Mm(line.GetEndPoint(1)) });
                }
            }

            var revolution = form as Revolution;
            if (revolution != null && i < lockAxis.Count && lockAxis[i])
            {
                var axisLine = revolution.Axis == null ? null : revolution.Axis.GeometryCurve as Line;
                if (axisLine == null || axisLine.Reference == null)
                {
                    skipped.Add(new { form = formId, reason = "ось вращения недоступна" });
                    continue;
                }

                var axisLocked = false;
                foreach (var plane in candidates)
                {
                    if (!LiesIn(axisLine, plane, normal, tolerance)) continue;
                    string error;
                    if (TryAlign(doc, view, plane, axisLine.Reference, out error))
                    {
                        axes.Add(new { form = formId, plane = plane.Name });
                        axisLocked = true;
                    }
                    else
                    {
                        skipped.Add(new { form = formId, plane = plane.Name, axis = true, error = error });
                    }
                }

                if (!axisLocked) free.Add(new { form = formId, axis = true, from = Mm(axisLine.GetEndPoint(0)), to = Mm(axisLine.GetEndPoint(1)) });
            }
        }

        return new { aligned = done.Count, axesLocked = axes.Count, done = done, axes = axes, free = free, skipped = skipped };
    }

    public static Sketch SketchOf(GenericForm form)
    {
        var extrusion = form as Extrusion;
        if (extrusion != null) return extrusion.Sketch;
        var revolution = form as Revolution;
        if (revolution != null) return revolution.Sketch;
        return null;
    }

    /// <summary>Линия лежит в опорной плоскости, и плоскость перпендикулярна плоскости эскиза.</summary>
    private static bool LiesIn(Line line, ReferencePlane plane, XYZ sketchNormal, double tolerance)
    {
        var n = plane.Normal.Normalize();
        if (Math.Abs(n.DotProduct(line.Direction)) > 1e-6) return false;
        if (Math.Abs(n.DotProduct(sketchNormal)) > 1e-6) return false;
        return Math.Abs(n.DotProduct(line.GetEndPoint(0) - plane.BubbleEnd)) <= tolerance;
    }

    private static bool TryAlign(Document doc, View view, ReferencePlane plane, Reference target, out string error)
    {
        error = null;
        try
        {
            var alignment = doc.FamilyCreate.NewAlignment(view, plane.GetReference(), target);
            if (alignment == null) error = "Revit не создал выравнивание";
            return alignment != null;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Вид, в котором плоскость эскиза видна плашмя (направление взгляда параллельно нормали эскиза).</summary>
    private static View FindView(Document doc, XYZ normal)
    {
        foreach (View view in new FilteredElementCollector(doc).OfClass(typeof(View)))
        {
            if (view.IsTemplate) continue;
            if (view.ViewType != ViewType.Elevation && view.ViewType != ViewType.FloorPlan && view.ViewType != ViewType.CeilingPlan && view.ViewType != ViewType.Section) continue;
            if (view.ViewDirection.CrossProduct(normal).GetLength() < 1e-6) return view;
        }

        return null;
    }

    private static double[] Mm(XYZ p)
    {
        return new[] { Math.Round(p.X * 304.8, 3), Math.Round(p.Y * 304.8, 3), Math.Round(p.Z * 304.8, 3) };
    }
}
