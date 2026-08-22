// ============================================================
//  СЮДА: Spectre/SpectreBridge.cs  (добавить НОВЫМ файлом)
//  Все методы, которые вызывает index.html. Атрибуты
//  ComVisible + AutoDual ОБЯЗАТЕЛЬНЫ, иначе WebView2
//  не увидит класс.
// ============================================================
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace Spectre;

[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public class SpectreBridge
{
    private readonly MainWindow _win;
    public SpectreBridge(MainWindow win) => _win = win;

    // ================= ВЫПОЛНИТЬ (кнопка / Ctrl+Enter) =================
    // Прилетает весь Lua-код активной вкладки.
    public void Execute(string lua)
    {
        // СЮДА — твой инжект (pipes / dll-инжектор / свой API):
        // MyInjector.Run(lua);

        _win.Send(new { t = "log", cls = "ok", msg = $"Получено {lua.Length} символов" });
    }

    // ================= ПОДКЛЮЧИТЬ / ОТКЛЮЧИТЬ =================
    public void Attach(int pid)
    {
        // Например:
        // var p = Process.GetProcessesByName("RobloxPlayerBeta").FirstOrDefault();
        // _win.Send(new { t = "log", cls = "ok", msg = $"Attach: PID {p?.Id}" });
    }

    public void Detach() { }

    // ================= ФАЙЛЫ (папка Scripts рядом с exe) =================
    public void SaveFile(string name, string code)
        => File.WriteAllText(PathOf(name), code);

    public void DeleteFile(string name)
        => File.Delete(PathOf(name));

    public void ListFiles()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Scripts");
        Directory.CreateDirectory(dir);
        var list = Directory.GetFiles(dir, "*.lua");
        // При желании отправь содержимое в UI:
        // _win.Send(new { t = "files", list = list.Select(f => new {
        //     name = Path.GetFileName(f), code = File.ReadAllText(f) }).ToArray() });
    }

    // Защита от выхода из папки через ../../
    private string PathOf(string file)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Scripts");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, Path.GetFileName(file));
    }

    // ================= КНОПКИ ОКНА =================
    public void Minimize() => _win.WindowState = WindowState.Minimized;

    public void CloseApp() => _win.Close();

    // ================= ПЕРЕТАСКИВАНИЕ ОКНА =================
    // UI шлёт mousedown по своему тайтлбару -> DragMove() ведёт окно
    // за курсором, пока кнопка мыши не отпущена.
    public void DragWindow() => _win.DragMove();
}
