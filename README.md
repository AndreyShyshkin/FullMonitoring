# FullMonitoring

Кроссплатформенная утилита системного и аппаратного мониторинга (Windows, Linux, macOS).  
Учебный проект в рамках курса по мультиплатформенной разработке.

---

## Технологический стек

- **Runtime:** .NET 10.0 (LTS)
- **UI Framework:** Avalonia UI (v11+)
- **Архитектурный паттерн:** MVVM (CommunityToolkit.Mvvm)
- **DI Container:** Microsoft.Extensions.DependencyInjection

---

## Архитектура проекта

Проект строится по принципу низкой связности (Decoupling) через паттерн **Strategy**:

1. **Core / Abstractions:** UI не работает с железом напрямую. Все модули телеметрии обращаются строго к интерфейсу `ITelemetryProvider`.
2. **Platform Providers:**
    - `WindowsProvider` — сбор через WMI / Performance Counters / LibreHardwareMonitorLib.
    - `LinuxProvider` — прямой I/O парсинг `/sys/class/hwmon` и `/proc/stat`.
    - `MacOsProvider` — `sysctl` (P/Invoke) / Fallback-метрики для Apple Silicon.
3. **UI Layer:** Views и ViewModels ничего не знают о текущей ОС. Отображение данных через реактивный Data Binding.

---

## Требования для локальной разработки

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- IDE на выбор:
    - JetBrains Rider (с установленным плагином *Avalonia for Rider*)
    - Visual Studio 2022 (компонент *.NET Desktop Development* + расширение *Avalonia*)
    - VS Code (с расширением *C# Dev Kit* и *Avalonia*)

---

## Быстрый старт

### Клонирование и сборка
```bash
git clone <URL_РЕПОЗИТОРИЯ>
cd FullMonitoring
dotnet restore
dotnet build
```

### Запуск приложения
```bash
dotnet run --project FullMonitoring/FullMonitoring.csproj
```