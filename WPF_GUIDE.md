# SPECTRE — инструкция по подключению UI к бэкенду на WPF (.NET 8)

Всё, что нужно со стороны UI — один файл `index.html` (из папки `dist` после сборки).
Интерфейс сам определяет, запущен он в WebView2 или в браузере:

- **в WebView2** — работает реальный мост (вызовы C#, инжект и т.д.);
- **в браузере** — включается демо-режим (файлы в localStorage, эмуляция выполнения), ничего не ломается.

---

## 1. Что понадобится

| Компонент | Версия |
|---|---|
| .NET SDK | 8.0+ |
| NuGet-пакет | `Microsoft.Web.WebView2` |
| WebView2 Runtime | ставится вместе с Windows 10/11 (проверка: `reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BEF-B90F0B0CF5F4}"`) |

Структура после сборки:

```
bin/Debug/net8.0-windows/
├── Spectre.exe
├── Spectre.dll
├── index.html        <-- наш интерфейс (просто скопировать рядом)
└── Scripts/          <-- workspace со скриптами (создаётся автоматически)
```

## 2. Создание проекта

```bat
dotnet new wpf -n Spectre
cd Spectre
dotnet add package Microsoft.Web.WebView2
dotnet build
```

Или руками в `Spectre.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.*" />
</ItemGroup>
```

## 3. MainWindow.xaml — окно без рамки, WebView2 на всё окно

```xml
<Window x:Class="Spectre.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:wv2="clr-namespace:Microsoft.Web.WebView2.Wpf;assembly=Microsoft.Web.WebView2.Wpf"
        Title="SPECTRE" Width="1080" Height="680"
        WindowStyle="None" ResizeMode="NoResize"
        Background="#0b0e15" WindowStartupLocation="CenterScreen">
    <Grid>
        <wv2:WebView2 x:Name="webView" DefaultBackgroundColor="#0b0e15"/>
    </Grid>
</Window>
```

- `WindowStyle="None"` — системной шапки нет, шапку рисует сам интерфейс.
- `DefaultBackgroundColor` = цвет `--bg` темы — не будет белой вспышки при старте.

## 4. MainWindow.xaml.cs — инициализация и мост

```csharp
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Spectre;

public partial class MainWindow : Window
{
    private CoreWebView2 _core;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await Init();
    }

    private async Task Init()
    {
        await webView.EnsureCoreWebView2Async();
        _core = webView.CoreWebView2;

        // ===== ЗАЩИТА ОТ ИНСПЕКЦИИ =====
        _core.Settings.AreDefaultContextMenusEnabled = false;   // ПКМ без «Просмотреть код»
        _core.Settings.AreDevToolsEnabled = false;              // F12 мертв
        _core.Settings.IsStatusBarEnabled = false;
        _core.Settings.AreBrowserAcceleratorKeysEnabled = false;

        // ===== МОСТ: UI вызывает методы C# как chrome.webview.hostObjects.spectre.Method(...) =====
        // ВАЖНО: регистрировать ДО навигации
        _core.AddHostObjectToScript("spectre", new SpectreBridge(this));

        // ===== РЕЗЕРВНЫЙ КАНАЛ UI -> C# (postMessage) =====
        _core.WebMessageReceived += (_, e) =>
        {
            string raw = e.TryGetWebMessageAsString();
            // разбор по желанию, например: JsonDocument.Parse(raw)
        };

        // ===== ОТКРЫВАЕМ ИНТЕРФЕЙС =====
        var html = Path.Combine(AppContext.BaseDirectory, "index.html");
        await _core.NavigateToFileAsync(html);
    }

    /// Отправка любой команды в UI
    public void Send(object obj) =>
        _core?.PostWebMessageAsString(JsonSerializer.Serialize(obj));
}
```

> Если нужен профиль с cookies/хранилищем, используй перегрузку
> `EnsureCoreWebView2Async(env, userDataFolder)`.

## 5. SpectreBridge.cs — все кнопки уже привязаны к этим методам

UI при старте ищет host-объект `spectre` и вызывает методы напрямую.
**Сторону UI менять не нужно** — просто реализуй методы:

```csharp
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

[ClassInterface(ClassInterfaceType.AutoDual)]  // ОБЯЗАТЕЛЬНО
[ComVisible(true)]                              // ОБЯЗАТЕЛЬНО
public class SpectreBridge
{
    private readonly MainWindow _win;
    public SpectreBridge(MainWindow win) => _win = win;

    // «ВЫПОЛНИТЬ» (Ctrl+Enter): сюда прилетает код активной вкладки
    public void Execute(string lua)
    {
        // сюда свой инжект: pipes / DLL / HTTP на инжектор
        _win.Send(new { t = "log", cls = "ok", msg = $"Получено {lua.Length} символов" });
    }

    // «ПОДКЛЮЧИТЬ» / «ОТКЛЮЧИТЬ»
    public void Attach(int pid)
    {
        // var roblox = Process.GetProcessesByName("RobloxPlayerBeta");
    }
    public void Detach() { }

    // «СОХРАНИТЬ» (Ctrl+S) — name уже содержит расширение (.lua)
    public void SaveFile(string name, string code) => File.WriteAllText(PathOf(name), code);

    // крестик у файла в боковой панели
    public void DeleteFile(string name) { if (File.Exists(PathOf(name))) File.Delete(PathOf(name)); }

    // «ОБНОВИТЬ» в боковой панели — пришли список обратно (раздел 6)
    public void ListFiles()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Scripts");
        Directory.CreateDirectory(dir);
        var list = Directory.GetFiles(dir, "*.lua");
        var items = Array.ConvertAll(list, f => new { name = Path.GetFileName(f), code = File.ReadAllText(f) });
        _win.Send(new { t = "files", list = items });
    }

    private string PathOf(string file)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Scripts");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, Path.GetFileName(file)); // режем ../ из имени
    }

    // КНОПКИ ОКНА
    public void Minimize() => _win.WindowState = WindowState.Minimized;
    public void CloseApp() => _win.Close();

    // ПЕРЕТАСКИВАНИЕ ОКНА (раздел 7)
    public void DragWindow() => _win.DragMove();
}
```

### Соответствие кнопок и методов

| Элемент UI | Метод моста |
|---|---|
| ВЫПОЛНИТЬ / Ctrl+Enter | `Execute(lua)` |
| Подключить / Отключить | `Attach(0)` / `Detach()` |
| Сохранить / Ctrl+S | `SaveFile(name, code)` |
| Удалить файл | `DeleteFile(name)` |
| Обновить файлы | `ListFiles()` |
| Свернуть окно | `Minimize()` |
| Закрыть окно | `CloseApp()` |
| mousedown по шапке | `DragWindow()` |

> Если в C# что-то упало, UI покажет «Мост [Метод]: ошибка…» в консоли — JS оборачивает каждый вызов в try/catch.

## 6. Команды WPF → UI

Шлём JSON через `Send()` (= `PostWebMessageAsString`). Поддерживается 4 типа:

```csharp
// лог в консоль (cls: out | info | ok | warn | err | sys)
win.Send(new { t = "log", msg = "Инжект успешен", cls = "ok" });

// открыть скрипт во вкладке (если вкладка с таким именем уже есть — она активируется, не дублируется)
win.Send(new { t = "open", name = "fly.lua", code = "print('hi')" });

// синхронизировать список файлов
win.Send(new { t = "files", list = new[] {
    new { name = "fly.lua", code = File.ReadAllText("Scripts/fly.lua") },
}});

// сменить тему из C# (переменные те же, что в панели «Темы»)
win.Send(new { t = "theme", vars = new Dictionary<string, string> {
    ["--acc"] = "#7cff3f", ["--bg"] = "#070a07", ["--bg2"] = "#0c120c"
}});
```

Сериализатор сам экранирует кавычки и переносы строк в Lua-коде — JSON руками собирать не надо.

Также для отладки можно вызвать UI напрямую из C#:
```csharp
await _core.ExecuteScriptAsync("SPECTRE.log('привет из C#','sys')");
// доступно: SPECTRE.openTab(name, code) / SPECTRE.log(msg, cls) / SPECTRE.execute() / SPECTRE.setTheme({vars})
```

## 7. Перетаскивание окна при WebView2 на весь Border

Системной шапки нет — «тайтлбар» нарисован внутри веб-страницы. Схема:

1. У шапки интерфейса есть `mousedown`-обработчик: если кликнули не по кнопке, вызывается `spectre.DragWindow()`.
2. Метод моста `DragWindow()` вызывает `Window.DragMove()`.
3. `DragMove()` **блокирует UI-поток, пока кнопка мыши не отпущена** — это нормальное поведение WPF: окно «прилипает» к курсору и едет за ним.
4. Методы host-объекта и так выполняются в UI-потоке, `Dispatcher` не нужен. Если вызываешь откуда-то ещё:

```csharp
public void DragWindow() => _win.Dispatcher.Invoke(() => _win.DragMove());
```

5. Размер окна зафиксирован (`ResizeMode="NoResize"`) — интерфейс рассчитан на один размер. Если нужно, верни `CanResize`, WebView2 растянется сам.

## 8. Анти-инспекция: правая кнопка и F12

**Уровень C# (надёжный)** — раздел 4:
```csharp
_core.Settings.AreDefaultContextMenusEnabled = false; // нет «Просмотреть код» по ПКМ
_core.Settings.AreDevToolsEnabled = false;            // F12 не откроется вообще
```

**Уровень JS (уже встроен в index.html):**
- перехват `contextmenu` по всему документу (своё меню вкладок работает — оно открывается своим обработчиком);
- перехват `F12`, `Ctrl+Shift+I/J/C`, `Ctrl+U`;
- ловушка `debugger` с замером времени — если DevTools всё же открыли, факт можно поймать и, например, закрыть окно.

**Дополнительно (по желанию):**
```csharp
// запретить навигацию куда-либо кроме нашего файла
_core.NavigationStarting += (_, e) =>
{
    if (!e.Uri.StartsWith("file:///")) e.Cancel = true;
};
```

## 9. Демо-режим и поведение вкладок

- Без WebView2 интерфейс работает автономно: «Подключить» эмулирует поиск процесса, «Выполнить» разбирает `print/warn/error` и выводит в консоль, файлы живут в `localStorage`.
- **Один файл = одна вкладка**: повторное открытие (из списка, из WPF-команды `open`, из drag&drop) активирует существующую вкладку, а не создаёт копию. Если пришёл новый код — содержимое обновляется и вкладка помечается «•».
- Дубликат создаётся только явно: ПКМ по вкладке → «Дублировать».

## 10. Частые проблемы

| Проблема | Решение |
|---|---|
| UI пишет «WPF не найден — демо-режим» внутри WebView2 | `AddHostObjectToScript` вызван **после** навигации или имя не `spectre` |
| Методы моста не видны из JS | На классе нет `[ComVisible(true)]` + `[ClassInterface(AutoDual)]`, или метод не `public` |
| Белый экран | Не установлен WebView2 Runtime; или `NavigateToFileAsync` без `await EnsureCoreWebView2Async()` |
| «Мост [Execute]: …» в консоли UI | Исключение в C#-методе — читай текст после двоеточия |
| Скрипты не переживают перезапуск в демо | Это localStorage браузера; в WebView2 храни свои файлы через `SaveFile` |
| Окно не перетаскивается | `DragMove()` вызывается вне UI-потока → оберни в `Dispatcher.Invoke` |

## 11. Быстрый чек-лист интеграции

1. [ ] `dotnet add package Microsoft.Web.WebView2`
2. [ ] XAML: `WindowStyle="None"`, `WebView2 x:Name="webView"` на всё окно
3. [ ] `EnsureCoreWebView2Async()` → настройки безопасности → `AddHostObjectToScript("spectre", new SpectreBridge(this))` → `NavigateToFileAsync(index.html)`
4. [ ] `SpectreBridge` с `[ComVisible(true)]` и всеми методами из раздела 5
5. [ ] `Send(...)` для логов/файлов/тем в UI
6. [ ] `index.html` лежит рядом с exe
