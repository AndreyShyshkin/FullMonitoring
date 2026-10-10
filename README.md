# FullMonitoring

Кросплатформна утиліта системного та апаратного моніторингу (Windows, Linux, macOS).  
Навчальний проєкт у межах курсу з мультиплатформної розробки.

---

## Технологічний стек

- **Runtime:** .NET 10.0 (LTS)
- **UI Framework:** Avalonia UI (v11+)
- **Архітектурний патерн:** MVVM (CommunityToolkit.Mvvm)
- **DI Container:** Microsoft.Extensions.DependencyInjection

---

## Архітектура проєкту

Проєкт будується за принципом низької зв'язності (Decoupling) із використанням патерну **Strategy**:

1. **Core / Abstractions:** UI не взаємодіє з апаратним забезпеченням напряму. Усі модулі телеметрії звертаються виключно до інтерфейсу `ITelemetryProvider`.
2. **Platform Providers:**
    - `BclTelemetryProvider` — базовий кросплатформний провайдер метрик (.NET BCL: `GC`, `DriveInfo`, `NetworkInterface`).
    - `MockTelemetryProvider` — провайдер із генерацією псевдовипадкових коливань для паралельної розробки UI та тестування.
    - `WindowsProvider` — збір через WMI / Performance Counters / LibreHardwareMonitorLib.
    - `LinuxTelemetryProvider` — нативний парсинг procfs/sysfs без сторонніх бінарних залежностей:
        - `/proc/stat` — загальна утилізація CPU та навантаження кожного логічного ядра за дельтою між `idle` та `total`;
        - `/proc/meminfo` — загальний і доступний обсяг RAM (`MemTotal`, `MemAvailable`);
        - `/sys/class/hwmon/` — рекурсивний пошук датчиків температури та швидкості обертання вентиляторів.
      Розбір рядків виконується над `ReadOnlySpan<char>` для мінімізації аллокацій у GC, а відсутність файлів (віртуальні машини, контейнери) не призводить до падіння.
    - `MacOsProvider` — `sysctl` (P/Invoke) / Fallback-метрики для Apple Silicon.
3. **Модуль процесів (Process Monitoring):**
    - `IProcessManagerService` — інтерфейс збору телеметрії та завершення процесів.
    - `ProcessManagerService` — реалізація на базі `System.Diagnostics.Process` (вибірка топ-50 процесів, сортування за спаданням оперативної пам'яті, безпечна обробка системних процесів та метод `KillProcess`).
    - `ProcessItem` — DTO-модель процесу (PID, назва, споживання RAM, кількість потоків, статус).
4. **UI Layer:** Views та ViewModels не мають прив'язки до поточної ОС. Відображення даних реалізовано через реактивний Data Binding:
    - `MainWindow` / `MainViewModel` — адаптивний головний каркас (Sidebar + Dashboard) та навігація між вкладками.
    - `OverviewView` / `OverviewViewModel` — зведені картки метрик CPU, RAM, Disk, Network.
    - `ProcessesView` / `ProcessesViewModel` — таблиця активних процесів із пошуком та керуванням.
    - `SettingsView` / `SettingsViewModel` — налаштування інтервалу опитування та теми оформлення.
    - `SystemHealthState` / `SystemStatePalette` — фіксована колірна палітра системних станів (нормальний, попередження, критичний).

---

## Вимоги для локальної розробки

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- IDE на вибір:
    - JetBrains Rider (із встановленим плагіном *Avalonia for Rider*)
    - Visual Studio 2022 (компонент *.NET Desktop Development* + розширення *Avalonia*)
    - VS Code (із розширеннями *C# Dev Kit* та *Avalonia*)

---

## Швидкий старт

### Клонування та збірка
```bash
git clone <URL_РЕПОЗИТОРІЮ>
cd FullMonitoring
dotnet restore
dotnet build
```

### Запуск тестів
```bash
dotnet test
```

### Запуск застосунку
```bash
dotnet run --project FullMonitoring/FullMonitoring.csproj
```
