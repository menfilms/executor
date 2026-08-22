// ============================================================
//  СЮДА: Spectre/MainWindow.xaml.cs  (заменить файл целиком)
//  Инициализация WebView2 + регистрация моста "spectre".
//  Именно имя "spectre" зашито в index.html — не менять.
// ============================================================
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Spectre;

public partial class MainWindow : Window
{
    private CoreWebView2? _core;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await Init();
    }

    private async Task Init()
    {
        // Ждём, пока WebView2 поднимет своё ядро Edge
        await webView.EnsureCoreWebView2Async();
        _core = webView.CoreWebView2;

        // ---------- Анти-инспекция ----------
        _core.Settings.AreDevToolsEnabled = false;              // F12 мёртв
        _core.Settings.AreDefaultContextMenusEnabled = false;   // ПКМ не покажет «Просмотреть код»
        _core.Settings.IsStatusBarEnabled = false;              // без строки состояния
        _core.Settings.AreBrowserAcceleratorKeysEnabled = false;// Ctrl+W / Ctrl+T браузера не мешают

        // ---------- Мост: UI вызывает методы C# как spectre.Method(...) ----------
        _core.AddHostObjectToScript("spectre", new SpectreBridge(this));

        // ---------- Резервный канал UI -> C# (postMessage) ----------
        _core.WebMessageReceived += (_, e) =>
        {
            string raw = e.TryGetWebMessageAsString() ?? "";
            // сюда, если нужен дополнительный канал (например, метрики)
        };

        // ---------- Открываем интерфейс (index.html рядом с exe) ----------
        var html = Path.Combine(AppContext.BaseDirectory, "index.html");
        await _core.NavigateToFileAsync(html);
    }

    /// Отправка любой команды в UI: log / open / files / theme
    public void Send(object obj) =>
        _core?.PostWebMessageAsString(JsonSerializer.Serialize(obj));
}
