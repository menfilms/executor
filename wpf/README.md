# Куда перекидывать файлы

Эта папка содержит **готовые части WPF-бэкенда**. Собирается проект за 5 минут.

## Карта проекта

```
Spectre/                          ← твой проект WPF (.NET 8)
│
├─ Spectre.csproj                 ← создаётся командой dotnet new wpf
│                                    + пакет Microsoft.Web.WebView2 (раздел «Шаг 1»)
│
├─ MainWindow.xaml                ← СЮДА: скопировать содержимое файла
│                                    wpf/MainWindow.xaml (заменить целиком)
│                                    Окно без рамки, размер зафиксирован (NoResize),
│                                    WebView2 растянут на всё окно
│
├─ MainWindow.xaml.cs             ← СЮДА: скопировать содержимое файла
│                                    wpf/MainWindow.xaml.cs (заменить целиком)
│                                    Инициализация WebView2, регистрация моста
│                                    "spectre", отключение F12/ПКМ, навигация
│
├─ SpectreBridge.cs               ← СЮДА: добавить как НОВЫЙ файл в проект
│                                    (ПКМ по проекту → Добавить → Класс)
│                                    Все методы, которые дёргает UI: Execute,
│                                    Attach, SaveFile, Minimize, DragWindow...
│
├─ index.html                     ← СЮДА: скопировать собранный dist/index.html
│                                    (или исходный index.html) — кладётся РЯДОМ
│                                    с exe, в папку bin/Debug/net8.0-windows/
│
└─ Scripts/                       ← создастся автоматически при первом
                                     сохранении файла из UI
```

## Шаг 1 — создать проект и поставить пакет

```bat
dotnet new wpf -n Spectre
cd Spectre
dotnet add package Microsoft.Web.WebView2
```

## Шаг 2 — раскидать файлы по карте выше

- `MainWindow.xaml` и `MainWindow.xaml.cs` — заменить содержимое существующих файлов.
- `SpectreBridge.cs` — добавить новым файлом.
- `index.html` — скопировать в корень проекта и добавить в `.csproj`, чтобы он копировался в bin:

```xml
<ItemGroup>
  <None Update="index.html">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## Шаг 3 — запуск

```bat
dotnet run
```

Интерфейс при старте сам найдёт мост `spectre` и переключится из демо-режима
в реальный. Если в консоли UI написано «Мост WPF найден» — всё подключено.

## Что уже привязано (ничего дописывать в UI не нужно)

| Действие в UI            | Метод в SpectreBridge.cs |
|--------------------------|--------------------------|
| Кнопка «Выполнить» / Ctrl+Enter | `Execute(string lua)` |
| Кнопка «Подключить»      | `Attach(int pid)`        |
| Кнопка «Отключить»       | `Detach()`               |
| Кнопка «Сохранить» / Ctrl+S | `SaveFile(name, code)` |
| Удаление файла (модалка) | `DeleteFile(name)`       |
| Кнопка «Обновить» файлы  | `ListFiles()`            |
| Кнопка «Свернуть»        | `Minimize()`             |
| Кнопка «Закрыть»         | `CloseApp()`             |
| Тянуть тайтлбар мышью    | `DragWindow()`           |

## Команды в обратную сторону (C# → UI)

```csharp
win.Send(new { t = "log",   msg = "Инжект успешен", cls = "ok" });          // в консоль
win.Send(new { t = "open",  name = "fly.lua", code = "print('hi')" });      // открыть во вкладке
win.Send(new { t = "files", list = new[] { new { name = "a.lua", code = "" } } }); // синхронизация файлов
win.Send(new { t = "theme", vars = new Dictionary<string,string>{ ["--acc"] = "#7cff3f" } }); // смена скина
```

Полная подробная инструкция — в `WPF_GUIDE.md` в корне проекта.
