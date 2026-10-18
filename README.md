# FullMonitoring

Кросплатформна утиліта системного та апаратного моніторингу (Windows, Linux, macOS).  
Навчальний проєкт у межах курсу з мультиплатформної розробки.

---

## Технологічний стек

- **Runtime:** .NET 10.0 (LTS)
- **UI Framework:** Avalonia UI (v11+)
- **Візуалізація графіків:** LiveCharts2 (`LiveChartsCore.SkiaSharpView.Avalonia`)
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
    - `MacOsTelemetryProvider` — нативний читач `sysctlbyname` (P/Invoke до `libc`) із чесним fallback на BCL-метрики:
        - `MacOsSysctlReader` — безпечна обгортка ключів `hw.memsize`, `hw.logicalcpu`, `machdep.cpu.brand_string` (на не-macOS повертає `false` без винятків);
        - загальний обсяг RAM уточнюється через `hw.memsize`, кількість ядер — через `hw.logicalcpu`;
        - температура CPU та оберти кулера повертають `null` (доступ до SMC обмежено політикою безпеки macOS) — UI показує бейдж `«Заблоковано політикою безпеки ОС»`.
3. **Механізм платформової резолюції (DI):**
    - `TelemetryServiceRegistration` — розширення `IServiceCollection.AddFullMonitoringPlatformServices()` для `Microsoft.Extensions.DependencyInjection`.
    - Резолюція `ITelemetryProvider` через `RuntimeInformation.IsOSPlatform`: `Windows` → `WindowsTelemetryProvider`, `Linux` → `LinuxTelemetryProvider`, `OSX` → `MacOsTelemetryProvider`, інакше — `BclTelemetryProvider`.
    - `CreateTelemetryProviderForCurrentOs()` — фабрика з безпечним fallback: якщо платформо-специфічний провайдер не створився, повертається базовий `BclTelemetryProvider` (режим Safe Mode без падіння).
    - `App.axaml.cs` лише будує `ServiceProvider` та розв'язує `MainViewModel`, `ITelemetryProvider`, `IAlertService`; звільнення ресурсів виконується у `OnShutdownRequested`.
    - `IPlatformInfoService` / `PlatformInfoService` — визначення платформи (`PlatformKind`: Windows / Linux / macOS / Unknown), перевірка прав адміністратора, режим `Safe Mode` та оцінка рівня доступу сенсорів (`SensorAccessLevel`: Full / Limited / Restricted).
3. **Модуль процесів (Process Monitoring):**
    - `IProcessManagerService` — інтерфейс збору телеметрії та завершення процесів.
    - `ProcessManagerService` — реалізація на базі `System.Diagnostics.Process` (вибірка топ-50 процесів, сортування за спаданням оперативної пам'яті, безпечна обробка системних процесів та метод `KillProcess`).
    - `ProcessItem` — DTO-модель процесу (PID, назва, споживання RAM, кількість потоків, статус).
4. **Система порогових сповіщень та журнал інцидентів (Alert & Threshold System):**
    - `ThresholdSettings` — модель конфігурації порогових значень із валідацією діапазонів:
        - Граничне завантаження CPU (за замовчуванням 90%).
        - Гранична температура CPU (за замовчуванням 85°C).
        - Граничне заповнення RAM (за замовчуванням 90%).
        - Порогова тривалість перебування вище норми (`DurationThresholdSeconds`, за замовчуванням 3 с) для усунення хибних спрацьовувань.
    - `IAlertService` / `AlertService` — сервіс аналізу метрик у реальному часі:
        - Відстеження безперервної тривалості перевантаження окремо для кожної метрики (CPU, RAM, температура).
        - Захист від дублювання (Debouncing): якщо показник стабільно тримається вище критичного ліміту, інцидент реєструється один раз, а повторні сповіщення щосекунди блокуються до повернення метрики в норму.
        - `AlertIncident` — модель події у журналі (унікальний ідентифікатор, мітка часу, тип метрики, зафіксоване та порогове значення, текстове повідомлення, статус вирішення).
        - Потокобезпечний журнал інцидентів з обмеженням розміру (буфер до 500 записів) та подіями нотифікації UI (`IncidentRaised`, `IncidentResolved`).
5. **UI Layer та візуалізація в реальному часі (LiveCharts2):** Views та ViewModels не мають прив'язки до поточної ОС. Відображення даних реалізовано через реактивний Data Binding:
    - `MainWindow` / `MainViewModel` — адаптивний головний каркас (Sidebar + Dashboard, мінімальний розмір `800x600` без перекриття елементів), бейдж кількості інцидентів, ненав'язлива панель сповіщень `InfoBar` для помилок провайдерів, синхронізація інтервалу опитування та теми оформлення, а також статус-бар платформи (`Платформа`, `Джерело`, `Сенсори`, `Безпечний режим (Safe Mode)` із прив'язкою до `IPlatformInfoService` та `RefreshPlatformStatus()`).
    - `OverviewView` / `OverviewViewModel` — зведені картки метрик CPU, RAM, Disk, Network із графіками в реальному часі:
        - Індикатори первинного завантаження (`Skeleton / Spinner`, `IsInitialLoading`), поки триває перший збір метрик при старті програми.
        - Візуальні підказки та бейджі `«Заблоковано політикою безпеки ОС»` для сенсорів (температура CPU, оберти кулера), доступ до яких обмежено правами ОС.
        - Лінійний графік динаміки утилізації CPU за останні 60 секунд (`CartesianChart` + `LineSeries<double>`).
        - Кругова діаграма (Donut `PieChart` + `PieSeries<ObservableValue>`) та горизонтальний `ProgressBar` розподілу оперативної пам'яті RAM (зайнято / вільно).
        - Таймер опитування `DispatcherTimer` (частота за замовчуванням 1 секунда) з оновленням колекцій і серій виключно в UI-потоці (`Dispatcher.UIThread`) та перехопленням помилок провайдера у `InfoBar` без падіння вікна.
    - `BoundedObservableBuffer<T>` — колекція `ObservableCollection<T>` фіксованого розміру (кільцевий буфер на 60 точок з автоматичним витісненням найстарішого елемента за індексом `0`), що запобігає витоку пам'яті під час тривалої роботи.
    - `ProcessesView` / `ProcessesViewModel` — таблиця активних процесів із пошуком, керуванням, індикацією блокування політикою безпеки ОС та `InfoBar`.
    - `EventsLogView` / `EventsLogViewModel` — журнал інцидентів із фільтрацією за типами метрик, часовими мітками та очищенням списку.
    - `SettingsView` / `SettingsViewModel` — налаштування інтервалу опитування, динамічне перемикання теми оформлення (`Dark / Light / System` через `ThemeVariant` і `ThemeDictionaries` в `App.axaml`) та порогів навантаження CPU/RAM/температури.
    - `SystemHealthState` / `SystemStatePalette` — фіксована колірна палітра системних станів (нормальний, попередження, критичний) та перевірка контрастності тексту і графіків за стандартом доступності **WCAG 2.1 AA**.
6. **Сервіс експорту та звітів телеметрії (Export Service & Reports — Issue #12):**
    - `TelemetryReport` — комплексна модель зрізу телеметрії системи (мітка часу, назва платформи, статус сенсорів, метрики CPU, RAM, накопичувачів, мережі, вибірка процесів та зафіксовані інциденти).
    - `IExportService` / `ExportService` — асинхронний потокобезпечний сервіс збереження даних:
        - `ExportToJsonAsync`: форматована JSON-серіалізація (`WriteIndented = true`, camelCase, безпечна підтримка Unicode через `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`).
        - `ExportToCsvAsync`: збереження табличних даних за стандартом RFC 4180 з додаванням мітки порядку байтів UTF-8 BOM (`new UTF8Encoding(true)`) для безпомилкового відображення кирилиці в Microsoft Excel та LibreOffice Calc; підтримка експорту процесів (`IEnumerable<ProcessItem>`), журналу подій (`IEnumerable<AlertIncident>`) та загального звіту телеметрії.
    - `IFileSaveDialogService` / `AvaloniaStorageFileSaveDialogService`: інтеграція з нативним діалогом вибору файлу збереження Avalonia через `StorageProvider` (`TopLevel.GetTopLevel(...)?.StorageProvider.SaveFilePickerAsync(...)`) з можливістю підміни у тестах для headless-перевірок.
    - Інтеграція кнопок експорту в інтерфейс застосунку:
        - **Дашборд (Overview):** збереження повного системного зрізу телеметрії (`TelemetryReport`) у форматах JSON або CSV.
        - **Таблиця процесів (Processes):** вивантаження активних процесів у JSON або CSV.
        - **Журнал подій (Events Log):** вивантаження історії зафіксованих інцидентів у JSON або CSV.
        - Неблокуюче асинхронне збереження з інформуванням користувача про успіх або помилку через статусний `InfoBar`.

---

## Вимоги для локальної розробки

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (мінімум `10.0.100`, зафіксовано у `global.json`)
- IDE на вибір:
    - JetBrains Rider (із встановленим плагіном *Avalonia for Rider*)
    - Visual Studio 2022 (компонент *.NET Desktop Development* + розширення *Avalonia*)
    - VS Code (із розширеннями *C# Dev Kit* та *Avalonia*)

> **Чому саме .NET 10 SDK?** Генератори XAML-коду Avalonia 12 (`Avalonia.Generators`) зібрані під Roslyn 4.14+.
> Збірка старішим SDK (наприклад, 9.0.1xx з Roslyn 4.12) мовчки не генерує метод `InitializeComponent`
> (попередження `CS9057`), і компіляція падає з помилками `CS0103` у всіх файлах `*.axaml.cs`.
> Файл `global.json` у корені репозиторію примусово вимагає SDK 10, щоб замість
> незрозумілих `CS0103` одразу було видно чесну вимогу до toolchain.

### Встановлення .NET 10 SDK на Ubuntu ARM64

```bash
# 1. Додати офіційний фід пакетів Microsoft (Ubuntu 24.04+, архітектура arm64)
wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# 2. Встановити SDK 10
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0

# 3. Перевірити версію (має бути 10.0.100 або новіше)
dotnet --version
```

Альтернатива без `apt` — скрипт встановлення у домашній каталог:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
dotnet --version
```

### Чиста збірка після зміни SDK або гілки

```bash
# Видалити артефакти попередніх збірок (особливо важливо на спільних
# каталогах типу Parallels Shared Folders, де bin/obj спільні між ОС)
rm -rf FullMonitoring/bin FullMonitoring/obj FullMonitoring.Tests/bin FullMonitoring.Tests/obj
dotnet restore
dotnet build
```

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

### Статичний аналіз коду та лінтинг

У проєкті налаштовано комплексний статичний аналіз коду на базі **SonarAnalyzer.CSharp** (SonarQube/SonarCloud правила для .NET), **Roslyn NetAnalyzers** та **dotnet format** (`.editorconfig`):

```bash
# Запуск комплексного статичного аналізу (Quality Gate: 100% / Рейтинг A)
./scripts/run-static-analysis.sh

# Або окремі кроки:
dotnet format --verify-no-changes
dotnet build FullMonitoring.sln --no-incremental /warnaserror
dotnet test FullMonitoring.sln
```

Детальний звіт із порівняльними метриками до та після усунення технічного боргу наведено у файлі [STATIC_ANALYSIS_REPORT.md](STATIC_ANALYSIS_REPORT.md).

