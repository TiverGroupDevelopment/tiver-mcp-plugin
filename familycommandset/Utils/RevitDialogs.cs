using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RevitMCPFamilyCommandSet.Utils;

/// <summary>Модальное окно Revit, найденное через Win32: заголовок, тексты и кнопки.</summary>
public sealed class RevitDialogInfo
{
    public long Handle { get; set; }
    public string Title { get; set; }
    public List<string> Texts { get; } = new List<string>();
    public List<string> Buttons { get; } = new List<string>();

    public object Describe() => new { handle = Handle, title = Title, texts = Texts, buttons = Buttons };

    public string Summary()
    {
        var text = Texts.Count == 0 ? string.Empty : ": " + string.Join(" / ", Texts.Take(3));
        return $"«{Title}»{text} [кнопки: {string.Join(", ", Buttons)}]";
    }
}

/// <summary>
/// Чтение и закрытие модальных окон Revit через Win32. Работает из потока сокета, пока главный поток
/// Revit крутит цикл сообщений модального окна, поэтому не зависит от ExternalEvent.
/// </summary>
public static class RevitDialogs
{
    private const uint WmGetText = 0x000D;
    private const uint BmClick = 0x00F5;
    private const uint SmtoAbortIfHung = 0x0002;

    private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowEnabled(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, StringBuilder lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>Видимые диалоги (#32770) процесса Revit.</summary>
    public static List<RevitDialogInfo> Find()
    {
        var result = new List<RevitDialogInfo>();
        uint pid;
        using (var process = Process.GetCurrentProcess()) pid = (uint)process.Id;

        try
        {
            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out var owner);
                if (owner != pid || !IsWindowVisible(hwnd) || ClassOf(hwnd) != "#32770") return true;

                var info = new RevitDialogInfo { Handle = hwnd.ToInt64(), Title = TextOf(hwnd) };
                EnumChildWindows(hwnd, (child, __) =>
                {
                    if (!IsWindowVisible(child)) return true;
                    var text = TextOf(child);
                    if (string.IsNullOrWhiteSpace(text)) return true;
                    var cls = ClassOf(child);
                    if (cls == "Button")
                    {
                        if (IsWindowEnabled(child)) info.Buttons.Add(text);
                    }
                    else if (!info.Texts.Contains(text))
                    {
                        info.Texts.Add(text.Trim());
                    }

                    return true;
                }, IntPtr.Zero);
                result.Add(info);
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception)
        {
        }

        return result;
    }

    /// <summary>Короткое описание открытых окон для сообщения об ошибке; пусто, если окон нет.</summary>
    public static string DescribeOpen()
    {
        var dialogs = Find();
        return dialogs.Count == 0 ? string.Empty : string.Join("; ", dialogs.Select(d => d.Summary()));
    }

    /// <summary>
    /// Нажимает кнопку в окне Revit. button — текст кнопки (без учёта «&amp;» и регистра);
    /// по умолчанию «Отмена»/Cancel, а если её нет — «Закрыть»/Close/«ОК».
    /// </summary>
    public static (bool clicked, string button, RevitDialogInfo dialog) Click(long? handle, string button)
    {
        var dialogs = Find();
        var dialog = handle.HasValue ? dialogs.FirstOrDefault(d => d.Handle == handle.Value) : dialogs.FirstOrDefault();
        if (dialog == null) return (false, null, null);

        var wanted = button != null
            ? new[] { button }
            : new[] { "Отмена", "Cancel", "Закрыть", "Close", "ОК", "OK", "Нет", "No" };

        IntPtr target = IntPtr.Zero;
        string pressed = null;
        foreach (var name in wanted)
        {
            EnumChildWindows(new IntPtr(dialog.Handle), (child, _) =>
            {
                if (ClassOf(child) != "Button" || !IsWindowVisible(child) || !IsWindowEnabled(child)) return true;
                var text = TextOf(child);
                if (string.Equals(Normalize(text), Normalize(name), StringComparison.OrdinalIgnoreCase))
                {
                    target = child;
                    pressed = text;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
            if (target != IntPtr.Zero) break;
        }

        if (target == IntPtr.Zero) return (false, null, dialog);
        PostMessage(target, BmClick, IntPtr.Zero, IntPtr.Zero);
        return (true, pressed, dialog);
    }

    private static string Normalize(string text) => (text ?? string.Empty).Replace("&", string.Empty).Trim();

    private static string ClassOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        GetClassName(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string TextOf(IntPtr hwnd)
    {
        var buffer = new StringBuilder(4096);
        SendMessageTimeout(hwnd, WmGetText, new IntPtr(buffer.Capacity), buffer, SmtoAbortIfHung, 500, out _);
        return buffer.ToString();
    }
}
