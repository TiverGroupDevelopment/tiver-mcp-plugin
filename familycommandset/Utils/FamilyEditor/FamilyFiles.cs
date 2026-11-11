using Autodesk.Revit.ApplicationServices;
using System.IO;

namespace RevitMCPFamilyCommandSet.Utils.FamilyEditor;

/// <summary>Пути шаблонов (.rft) и файлов семейств (.rfa).</summary>
public static class FamilyFiles
{
    public static string TemplateRoot(Application application)
    {
        var root = application.FamilyTemplatePath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            throw new InvalidOperationException(
                $"Папка шаблонов семейств не найдена ('{root}'). Проверьте «Параметры → Файлы → Путь к шаблонам семейств» или передайте полный путь к .rft.");
        }

        return root.TrimEnd('\\', '/');
    }

    public static List<string> ListTemplates(string root)
    {
        return Directory.EnumerateFiles(root, "*.rft", SearchOption.AllDirectories)
            .Select(file => file.Substring(root.Length).TrimStart('\\', '/'))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Полный путь, путь относительно папки шаблонов или имя файла шаблона (с .rft или без).</summary>
    public static string ResolveTemplate(Application application, string template)
    {
        var withExtension = template.EndsWith(".rft", StringComparison.OrdinalIgnoreCase) ? template : template + ".rft";
        if (Path.IsPathRooted(withExtension))
        {
            return File.Exists(withExtension)
                ? Path.GetFullPath(withExtension)
                : throw new ArgumentException($"Шаблон '{withExtension}' не найден");
        }

        var root = TemplateRoot(application);
        var direct = Path.Combine(root, withExtension);
        if (File.Exists(direct)) return Path.GetFullPath(direct);

        var fileName = Path.GetFileName(withExtension);
        var matches = ListTemplates(root)
            .Where(relative => string.Equals(Path.GetFileName(relative), fileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 1) return Path.Combine(root, matches[0]);
        if (matches.Count > 1)
        {
            throw new ArgumentException($"Шаблон '{template}' найден в нескольких папках: {string.Join("; ", matches)}. Укажите путь относительно '{root}'.");
        }

        throw new ArgumentException($"Шаблон '{template}' не найден в '{root}'. Список шаблонов — list_family_templates.");
    }

    /// <summary>Проверяет путь сохранения .rfa и возвращает полный путь и признак существования файла.</summary>
    public static string ValidateSavePath(string path, bool overwrite, out bool existed)
    {
        if (!path.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Путь сохранения должен оканчиваться на .rfa");
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            throw new ArgumentException($"Папка '{directory}' не существует");
        }

        existed = File.Exists(fullPath);
        if (existed && !overwrite)
        {
            throw new InvalidOperationException($"Файл '{fullPath}' уже существует. Для замены передайте overwrite: true");
        }

        return fullPath;
    }
}
