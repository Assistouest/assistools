using System;
using System.Collections.Generic;
using System.Diagnostics;
using LibreHardwareMonitor.Hardware;

namespace Assistools.Services;

public class HardwareReadings
{
    public double? CpuTemperatureC { get; set; }
    public double? CpuLoadPercent { get; set; }
    public double? CpuPowerW { get; set; }
    public double? MotherboardTemperatureC { get; set; }
    public Dictionary<string, double> GpuTemperatures { get; } = new();
    public Dictionary<string, double> GpuLoads { get; } = new();
    public Dictionary<string, long> GpuVramUsedBytes { get; } = new();
    public Dictionary<string, double> StorageTemperatures { get; } = new();
    public Dictionary<string, int> StorageWearLevels { get; } = new();
}

public static class HardwareMonitor
{
    public static HardwareReadings Read()
    {
        var readings = new HardwareReadings();
        try
        {
            var computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsStorageEnabled = true,
                IsMemoryEnabled = true,
            };
            computer.Open();
            try
            {
                foreach (var hardware in computer.Hardware)
                {
                    hardware.Update();
                    ReadHardware(hardware, readings);
                    foreach (var sub in hardware.SubHardware)
                    {
                        sub.Update();
                        ReadHardware(sub, readings);
                    }
                }
            }
            finally
            {
                computer.Close();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HardwareMonitor] Erreur : {ex.Message}");
        }
        return readings;
    }

    private static void ReadHardware(IHardware hardware, HardwareReadings r)
    {
        foreach (var s in hardware.Sensors)
        {
            if (!s.Value.HasValue) continue;
            double v = s.Value.Value;

            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    if (s.SensorType == SensorType.Temperature &&
                        (s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Contains("CPU Package", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Equals("Core Average", StringComparison.OrdinalIgnoreCase)))
                    {
                        r.CpuTemperatureC ??= v;
                    }
                    else if (s.SensorType == SensorType.Load &&
                             s.Name.Equals("CPU Total", StringComparison.OrdinalIgnoreCase))
                    {
                        r.CpuLoadPercent = v;
                    }
                    else if (s.SensorType == SensorType.Power &&
                             s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                    {
                        r.CpuPowerW ??= v;
                    }
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    if (s.SensorType == SensorType.Temperature &&
                        s.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                    {
                        r.GpuTemperatures[hardware.Name] = v;
                    }
                    else if (s.SensorType == SensorType.Load &&
                             s.Name.Equals("GPU Core", StringComparison.OrdinalIgnoreCase))
                    {
                        r.GpuLoads[hardware.Name] = v;
                    }
                    else if (s.SensorType == SensorType.SmallData &&
                             s.Name.Contains("GPU Memory Used", StringComparison.OrdinalIgnoreCase))
                    {
                        // SmallData en Mo
                        r.GpuVramUsedBytes[hardware.Name] = (long)(v * 1024 * 1024);
                    }
                    break;

                case HardwareType.Storage:
                    if (s.SensorType == SensorType.Temperature)
                    {
                        r.StorageTemperatures[hardware.Name] = v;
                    }
                    else if (s.SensorType == SensorType.Level &&
                             s.Name.Contains("Remaining Life", StringComparison.OrdinalIgnoreCase))
                    {
                        r.StorageWearLevels[hardware.Name] = (int)(100 - v);
                    }
                    break;

                case HardwareType.Motherboard:
                case HardwareType.SuperIO:
                    if (s.SensorType == SensorType.Temperature &&
                        (s.Name.Contains("Motherboard", StringComparison.OrdinalIgnoreCase) ||
                         s.Name.Equals("Temperature #1", StringComparison.OrdinalIgnoreCase)))
                    {
                        r.MotherboardTemperatureC ??= v;
                    }
                    break;
            }
        }
    }
}
