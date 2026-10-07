using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ConstiBoost.Logging;
using ConstiBoost.Models;

namespace ConstiBoost.Services;

public sealed record CleanResult(long FreedBytes, int DeletedFiles, int SkippedFiles);

/// <summary>
/// Deletes only well-known cache/temp folders. A path guard refuses everything outside those roots
/// and everything inside personal folders (Documents, Pictures, Desktop, Downloads, saved games…).
/// Reparse points (junctions/symlinks) are never followed.
/// </summary>
public static class CleanerService
{
    private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    private static readonly string WindowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static readonly string[] BrowserProcesses = { "chrome", "msedge", "brave", "firefox", "opera" };

    public static List<CleanupCategory> GetCategories()
    {
        return new List<CleanupCategory>
        {
            new()
            {
                Id = "temp",
                Name = "Temporary Files",
                Icon = "\uE74D",
                Description = "Your user temp folder (%TEMP%). Files changed in the last 24 hours are kept so running installers are not affected.",
                Paths = { Path.GetTempPath() },
                MinAgeHours = 24,
            },
            new()
            {
                Id = "wintemp",
                Name = "Windows Temporary Files",
                Icon = "\uE7B8",
                Description = @"C:\Windows\Temp. Requires administrator permission (UAC). Files changed in the last 24 hours are kept.",
                AdminPaths = { Path.Combine(WindowsDir, "Temp") },
                MinAgeHours = 24,
            },
            new()
            {
                Id = "shader",
                Name = "Shader Cache",
                Icon = "\uE7F4",
                Description = "DirectX and GPU driver shader caches. Windows/drivers rebuild them automatically – the first game start afterwards can stutter briefly. Useful after driver updates or when games stutter permanently.",
                Paths = ShaderCachePaths(),
                AdminPaths = { Path.Combine(ProgramData, "NVIDIA Corporation", "NV_Cache") },
            },
            new()
            {
                Id = "crash",
                Name = "Crash Reports",
                Icon = "\uE7BA",
                Description = "Windows Error Reporting archives and app crash dumps. Only needed if you want to send a crash report to a developer.",
                Paths =
                {
                    Path.Combine(Local, "CrashDumps"),
                    Path.Combine(Local, "Microsoft", "Windows", "WER", "ReportArchive"),
                    Path.Combine(Local, "Microsoft", "Windows", "WER", "ReportQueue"),
                },
                AdminPaths =
                {
                    Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportArchive"),
                    Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportQueue"),
                },
            },
            new()
            {
                Id = "browser",
                Name = "Browser Cache",
                Icon = "\uE774",
                Description = "Cached web files of Chrome, Edge, Brave and Firefox. Cookies, passwords, history and logins are NOT touched or read. Close your browser first – files in use are skipped.",
                Paths = BrowserCachePaths(),
                IsSelected = false,
            },
        };
    }

    public static List<string> ShaderCachePaths()
    {
        string localLow = Path.Combine(UserProfile, "AppData", "LocalLow");
        return new List<string>
        {
            Path.Combine(Local, "D3DSCache"),
            Path.Combine(Local, "AMD", "DxCache"),
            Path.Combine(Local, "AMD", "DxcCache"),
            Path.Combine(Local, "AMD", "GLCache"),
            Path.Combine(Local, "AMD", "VkCache"),
            Path.Combine(Local, "NVIDIA", "DXCache"),
            Path.Combine(Local, "NVIDIA", "GLCache"),
            Path.Combine(localLow, "Intel", "ShaderCache"),
        };
    }

    private static List<string> BrowserCachePaths()
    {
        var paths = new List<string>();
        var chromium = new[]
        {
            Path.Combine(Local, "Google", "Chrome", "User Data"),
            Path.Combine(Local, "Microsoft", "Edge", "User Data"),
            Path.Combine(Local, "BraveSoftware", "Brave-Browser", "User Data"),
        };
        foreach (string userData in chromium)
        {
            try
            {
                if (!Directory.Exists(userData))
                {
                    continue;
                }

                foreach (string profile in Directory.EnumerateDirectories(userData))
                {
                    string name = Path.GetFileName(profile);
                    if (name != "Default" && !name.StartsWith("Profile ", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    paths.Add(Path.Combine(profile, "Cache", "Cache_Data"));
                    paths.Add(Path.Combine(profile, "Code Cache"));
                    paths.Add(Path.Combine(profile, "GPUCache"));
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn("Browser profiles unreadable: " + ex.Message);
            }
        }

        try
        {
            string firefox = Path.Combine(Local, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(firefox))
            {
                paths.AddRange(Directory.EnumerateDirectories(firefox).Select(p => Path.Combine(p, "cache2")));
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Firefox profiles unreadable: " + ex.Message);
        }

        return paths;
    }

    /// <summary>Folders the elevated helper is allowed to clean.</summary>
    public static IReadOnlyList<string> ElevatedRoots() => new[]
    {
        Path.Combine(WindowsDir, "Temp"),
        Path.Combine(ProgramData, "NVIDIA Corporation", "NV_Cache"),
        Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportArchive"),
        Path.Combine(ProgramData, "Microsoft", "Windows", "WER", "ReportQueue"),
    };

    private static IEnumerable<string> ForbiddenRoots()
    {
        var folders = new[]
        {
            Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.MyPictures, Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyMusic, Environment.SpecialFolder.MyVideos, Environment.SpecialFolder.Favorites,
        };
        foreach (var f in folders)
        {
            string path = Environment.GetFolderPath(f);
            if (!string.IsNullOrEmpty(path))
            {
                yield return path;
            }
        }

        yield return Path.Combine(UserProfile, "Downloads");
        yield return Path.Combine(UserProfile, "Saved Games");
        yield return Path.Combine(UserProfile, "OneDrive");
    }

    public static bool IsPathSafe(string path)
    {
        try
        {
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            if (full.Length <= 3)
            {
                return false; // never a drive root
            }

            if (ForbiddenRoots().Any(f => full.StartsWith(f.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var allowed = GetCategories().SelectMany(c => c.Paths.Concat(c.AdminPaths)).Concat(ElevatedRoots());
            return allowed.Any(a => string.Equals(full, Path.GetFullPath(a).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public static (long bytes, int files) Measure(string root, double minAgeHours)
    {
        long bytes = 0;
        int files = 0;
        foreach (var file in EnumerateFilesSafe(root))
        {
            try
            {
                if (minAgeHours > 0 && (DateTime.Now - file.LastWriteTime).TotalHours < minAgeHours)
                {
                    continue;
                }

                bytes += file.Length;
                files++;
            }
            catch
            {
                // skip unreadable file
            }
        }

        return (bytes, files);
    }

    public static CleanResult DeleteContents(string root, double minAgeHours = 0)
    {
        if (!IsPathSafe(root) || !Directory.Exists(root))
        {
            if (Directory.Exists(root))
            {
                AppLog.Warn("Cleaner refused unsafe path: " + root);
            }

            return new CleanResult(0, 0, 0);
        }

        long freed = 0;
        int deleted = 0;
        int skipped = 0;
        foreach (var file in EnumerateFilesSafe(root).ToList())
        {
            try
            {
                if (minAgeHours > 0 && (DateTime.Now - file.LastWriteTime).TotalHours < minAgeHours)
                {
                    skipped++;
                    continue;
                }

                long length = file.Length;
                if ((file.Attributes & FileAttributes.ReadOnly) != 0)
                {
                    file.Attributes &= ~FileAttributes.ReadOnly;
                }

                file.Delete();
                freed += length;
                deleted++;
            }
            catch
            {
                skipped++; // in use or access denied – left alone
            }
        }

        RemoveEmptyDirectories(root, isRoot: true);
        return new CleanResult(freed, deleted, skipped);
    }

    private static void RemoveEmptyDirectories(string dir, bool isRoot)
    {
        try
        {
            foreach (string sub in Directory.EnumerateDirectories(dir))
            {
                var info = new DirectoryInfo(sub);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                RemoveEmptyDirectories(sub, false);
            }

            if (!isRoot && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir, false);
            }
        }
        catch
        {
            // ignore – folder in use
        }
    }

    private static IEnumerable<FileInfo> EnumerateFilesSafe(string root)
    {
        var stack = new Stack<DirectoryInfo>();
        try
        {
            var start = new DirectoryInfo(root);
            if (!start.Exists)
            {
                yield break;
            }

            stack.Push(start);
        }
        catch
        {
            yield break;
        }

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            FileSystemInfo[] entries;
            try
            {
                entries = dir.GetFileSystemInfos();
            }
            catch
            {
                continue;
            }

            foreach (var entry in entries)
            {
                if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue; // never follow junctions/symlinks
                }

                if (entry is DirectoryInfo sub)
                {
                    stack.Push(sub);
                }
                else if (entry is FileInfo file)
                {
                    yield return file;
                }
            }
        }
    }

    public static IReadOnlyList<string> RunningBrowsers()
    {
        var running = new List<string>();
        foreach (string name in BrowserProcesses)
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                if (processes.Length > 0)
                {
                    running.Add(name);
                }

                foreach (var p in processes)
                {
                    p.Dispose();
                }
            }
            catch
            {
                // ignore
            }
        }

        return running;
    }
}
