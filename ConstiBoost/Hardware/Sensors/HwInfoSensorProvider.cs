using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.RegularExpressions;

namespace ConstiBoost.Hardware.Sensors;

/// <summary>
/// Reads sensor values from HWiNFO's documented shared memory ("Shared Memory Support" must be enabled in HWiNFO).
/// Read-only: ConstiBoost never installs a driver and never writes to the shared memory.
/// </summary>
public sealed class HwInfoSensorProvider : ISensorProvider
{
    private const string MapName = @"Global\HWiNFO_SENS_SM2";
    private const uint SignatureActive = 0x53695748; // "HWiS"

    private const int SensorReadingTemp = 1;
    private const int SensorReadingFan = 3;
    private const int SensorReadingClock = 6;

    private static readonly Regex CpuTempLabel = new(@"Tctl|Tdie|CPU Package|CPU \(Tctl|Core Temperatures|CPU Temperature|CPU$", RegexOptions.IgnoreCase);
    private static readonly Regex GpuTempLabel = new(@"^GPU (Temperature|Core|SoC)|GPU Temperature", RegexOptions.IgnoreCase);
    private static readonly Regex GpuClockLabel = new(@"^GPU (Clock|Core Clock|Gfx Clock)", RegexOptions.IgnoreCase);

    public string Name => "HWiNFO";

    public SensorSnapshot? Read()
    {
        try
        {
            using var mmf = Open();
            using var view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            if (view.ReadUInt32(0) != SignatureActive)
            {
                return null;
            }

            // Header layout (packed): signature, version, revision, poll time (int64),
            // offset/size/count of sensor elements, offset/size/count of reading elements.
            uint readingOffset = view.ReadUInt32(32);
            uint readingSize = view.ReadUInt32(36);
            uint readingCount = view.ReadUInt32(40);
            if (readingSize < 316 || readingCount == 0 || readingCount > 10000)
            {
                return null;
            }

            double? cpuTemp = null;
            string? cpuLabel = null;
            int cpuRank = int.MaxValue;
            double? gpuTemp = null;
            double? gpuClock = null;
            var fans = new List<SensorValue>();

            for (uint i = 0; i < readingCount; i++)
            {
                long pos = readingOffset + (long)i * readingSize;
                int type = view.ReadInt32(pos);
                if (type != SensorReadingTemp && type != SensorReadingFan && type != SensorReadingClock)
                {
                    continue;
                }

                string label = ReadString(view, pos + 12, 128);
                double value = view.ReadDouble(pos + 284);
                if (double.IsNaN(value))
                {
                    continue;
                }

                switch (type)
                {
                    case SensorReadingTemp when value is > 1 and < 125:
                        int rank = CpuRank(label);
                        if (rank < cpuRank)
                        {
                            cpuRank = rank;
                            cpuTemp = value;
                            cpuLabel = label;
                        }

                        if (gpuTemp is null && GpuTempLabel.IsMatch(label))
                        {
                            gpuTemp = value;
                        }

                        break;
                    case SensorReadingFan when value >= 0 && value < 20000:
                        fans.Add(new SensorValue(label, value, "RPM", Name));
                        break;
                    case SensorReadingClock when gpuClock is null && GpuClockLabel.IsMatch(label) && value > 0:
                        gpuClock = value;
                        break;
                }
            }

            if (cpuTemp is null && gpuTemp is null && fans.Count == 0 && gpuClock is null)
            {
                return null;
            }

            return new SensorSnapshot
            {
                CpuTemperature = cpuTemp,
                CpuTemperatureLabel = cpuLabel,
                GpuTemperature = gpuTemp,
                GpuClockMhz = gpuClock,
                Fans = fans,
                Provider = Name,
            };
        }
        catch (FileNotFoundException)
        {
            return null; // HWiNFO not running or shared memory disabled
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static MemoryMappedFile Open()
    {
        try
        {
            return MemoryMappedFile.OpenExisting(MapName, MemoryMappedFileRights.Read);
        }
        catch (FileNotFoundException)
        {
            return MemoryMappedFile.OpenExisting("HWiNFO_SENS_SM2", MemoryMappedFileRights.Read);
        }
    }

    private static int CpuRank(string label)
    {
        if (!CpuTempLabel.IsMatch(label))
        {
            return int.MaxValue;
        }

        if (label.Contains("Tctl", StringComparison.OrdinalIgnoreCase) || label.Contains("Tdie", StringComparison.OrdinalIgnoreCase)) return 0;
        if (label.Contains("Package", StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }

    private static string ReadString(MemoryMappedViewAccessor view, long position, int length)
    {
        var bytes = new byte[length];
        view.ReadArray(position, bytes, 0, length);
        int end = Array.IndexOf(bytes, (byte)0);
        return Encoding.Latin1.GetString(bytes, 0, end < 0 ? length : end).Trim();
    }
}
