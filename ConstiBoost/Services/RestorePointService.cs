using System;
using System.Management;
using System.Threading.Tasks;
using ConstiBoost.Logging;

namespace ConstiBoost.Services;

/// <summary>Creates a Windows System Restore point through the documented SystemRestore WMI class.</summary>
public static class RestorePointService
{
    private const int ModifySettings = 12;
    private const int BeginSystemChange = 100;

    public static async Task<ElevatedResult> CreateAsync(string description)
    {
        var result = await ElevationService.RunAsync(new ElevatedTask
        {
            Operation = ElevatedOperations.CreateRestorePoint,
            Description = description,
        });

        if (result.Success)
        {
            ActionLog.Success("Create restore point", null, description);
        }
        else
        {
            ActionLog.Failed("Create restore point", null, description, result.Message);
        }

        return result;
    }

    public static ElevatedResult CreateInProcess(string description)
    {
        int before = CountRestorePoints();
        try
        {
            using var restore = new ManagementClass(new ManagementScope(@"\\.\root\default"), new ManagementPath("SystemRestore"), null);
            using var inParams = restore.GetMethodParameters("CreateRestorePoint");
            inParams["Description"] = description.Length > 64 ? description[..64] : description;
            inParams["RestorePointType"] = ModifySettings;
            inParams["EventType"] = BeginSystemChange;
            using var outParams = restore.InvokeMethod("CreateRestorePoint", inParams, null);
            uint code = Convert.ToUInt32(outParams?["ReturnValue"] ?? 1u);
            if (code != 0)
            {
                return new ElevatedResult
                {
                    Success = false,
                    Message = code switch
                    {
                        1058 => "System Protection is turned off. Enable it under 'Create a restore point' (SystemPropertiesProtection).",
                        _ => $"Windows refused to create a restore point (code {code}). System Protection may be disabled for the system drive.",
                    },
                };
            }
        }
        catch (ManagementException ex)
        {
            return new ElevatedResult { Success = false, Message = "System Restore is not available: " + ex.Message };
        }
        catch (UnauthorizedAccessException)
        {
            return new ElevatedResult { Success = false, Message = "Creating a restore point requires administrator rights." };
        }

        int after = CountRestorePoints();
        if (before >= 0 && after >= 0 && after <= before)
        {
            return new ElevatedResult
            {
                Success = false,
                Message = "Windows skipped the restore point. By default Windows allows only one restore point every 24 hours, or System Protection is off.",
            };
        }

        return new ElevatedResult { Success = true, Message = "Restore point created." };
    }

    private static int CountRestorePoints()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\default", "SELECT SequenceNumber FROM SystemRestore");
            using var results = searcher.Get();
            return results.Count;
        }
        catch
        {
            return -1;
        }
    }
}
