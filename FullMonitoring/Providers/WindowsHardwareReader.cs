using System;
using System.Linq;
using System.Security.Principal;
using LibreHardwareMonitor.Hardware;
using System.Runtime.Versioning;

namespace FullMonitoring.Providers;

[SupportedOSPlatform("windows")]
public class WindowsHardwareReader : IDisposable
{
    private readonly Computer _computer;
    private bool _disposed;

    public bool IsAdmin { get; }

    public WindowsHardwareReader()
    {
        IsAdmin = CheckIsAdministrator();

        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = IsAdmin,
            IsControllerEnabled = IsAdmin,
            IsStorageEnabled = true
        };

        try
        {
            _computer.Open();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Driver init fallback: {ex.Message}");
        }
    }

    private static bool CheckIsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void Update()
    {
        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
            {
                subHardware.Update();
            }
        }
    }

    public (float? TotalLoad, float[] CoreLoads) GetCpuLoads()
    {
        var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu == null) return (null, Array.Empty<float>());

        var total = cpu.Sensors
            .FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total"))?.Value;

        var cores = cpu.Sensors
            .Where(s => s.SensorType == SensorType.Load && s.Name.Contains("Core #"))
            .Select(s => s.Value ?? 0f)
            .ToArray();

        return (total, cores);
    }

    public float? GetCpuTemperature()
    {
        var cpu = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        return cpu?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.Value;
    }

    public float? GetGpuTemperature()
    {
        var gpu = _computer.Hardware.FirstOrDefault(h => 
            h.HardwareType == HardwareType.GpuNvidia || 
            h.HardwareType == HardwareType.GpuAmd || 
            h.HardwareType == HardwareType.GpuIntel);

        return gpu?.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.Value;
    }

    public float? GetFanSpeed()
    {
        return _computer.Hardware
            .SelectMany(h => h.Sensors)
            .FirstOrDefault(s => s.SensorType == SensorType.Fan)?.Value;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _computer.Close();
            }
            _disposed = true;
        }
    }

    ~WindowsHardwareReader() => Dispose(false);
}