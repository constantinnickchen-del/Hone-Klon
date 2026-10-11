using System;
using System.ComponentModel;
using System.Threading.Tasks;
using ConstiBoost.Core;
using ConstiBoost.Logging;
using ConstiBoost.WinSys;

namespace ConstiBoost.Services;

/// <summary>Writes the processor maximum state in-process, falling back to the allow-listed elevated helper.</summary>
public static class PowerTweakService
{
    public static async Task<OperationResult> SetProcessorMaxStateAsync(Guid scheme, uint ac, uint dc, string action, bool allowElevation = true)
    {
        var before = PowerManager.GetProcessorMaxState(scheme);
        string oldText = before is null ? "unknown" : $"AC {before.Value.ac} % / DC {before.Value.dc} %";
        string newText = $"AC {ac} % / DC {dc} %";
        try
        {
            PowerManager.SetProcessorMaxState(scheme, ac, dc);
            ActionLog.Success(action, oldText, newText);
            return OperationResult.Ok(newText);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5 && !allowElevation)
        {
            ActionLog.Failed(action, oldText, newText, "Access denied (no UAC prompt in automatic mode)");
            return OperationResult.Fail("Access denied", "Windows requires administrator permission for this setting.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            var result = await ElevationService.RunAsync(new ElevatedTask
            {
                Operation = ElevatedOperations.ProcessorMaxState,
                PowerScheme = scheme.ToString(),
                AcValue = ac,
                DcValue = dc,
            });
            if (result.Success)
            {
                ActionLog.Success(action + " (admin)", oldText, newText);
                return OperationResult.Ok(newText);
            }

            ActionLog.Failed(action, oldText, newText, result.Message);
            return OperationResult.Fail("This optimization couldn't be applied.", result.Message);
        }
        catch (Exception ex)
        {
            ActionLog.Failed(action, oldText, newText, Safe.Describe(ex));
            return OperationResult.Fail("This optimization couldn't be applied.", Safe.Describe(ex));
        }
    }
}
