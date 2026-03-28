using System;
using System.IO;
using System.Threading.Tasks;

namespace TeddyBench.Avalonia.Services;

/// <summary>
/// Service for managing LIVE flags (DOS Hidden attribute) on Tonie files.
/// LIVE flag prevents auto-resume on Toniebox when figurine is removed/replaced.
/// </summary>
public class LiveFlagService
{
    /// <summary>
    /// Checks if a file has the Hidden attribute (LIVE flag) set.
    /// </summary>
    public bool GetHiddenAttribute(string filePath)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                // On Linux, use fatattr to check DOS hidden attribute
                var result = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "fatattr",
                    Arguments = $"\"{filePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (result != null)
                {
                    result.WaitForExit();
                    string output = result.StandardOutput.ReadToEnd();
                    // fatattr output format: "h" for hidden, "-" for not hidden
                    return output.Contains("h");
                }
            }
            else
            {
                // On Windows, use FileInfo.Attributes
                var fileInfo = new FileInfo(filePath);
                return fileInfo.Attributes.HasFlag(FileAttributes.Hidden);
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    /// <summary>
    /// Checks if a file has the Hidden attribute (LIVE flag) set asynchronously.
    /// </summary>
    public async Task<bool> GetHiddenAttributeAsync(string filePath)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                // On Linux, use fatattr to check DOS hidden attribute
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "fatattr",
                        Arguments = $"\"{filePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // fatattr output format: "h" for hidden, "-" for not hidden
                return output.Contains("h");
            }
            else
            {
                // On Windows, use FileInfo.Attributes (runs on thread pool)
                return await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(filePath);
                    return fileInfo.Attributes.HasFlag(FileAttributes.Hidden);
                });
            }
        }
        catch
        {
            // Ignore errors
        }

        return false;
    }

    /// <summary>
    /// Sets or removes the Hidden attribute (LIVE flag) on a file.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    public bool SetHiddenAttribute(string filePath, bool hidden)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                // On Linux, use fatattr to set DOS hidden attribute
                string arguments = hidden ? $"+h \"{filePath}\"" : $"-h \"{filePath}\"";

                var result = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "fatattr",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (result != null)
                {
                    result.WaitForExit();
                    return result.ExitCode == 0;
                }
            }
            else
            {
                // On Windows, use FileInfo.Attributes
                var fileInfo = new FileInfo(filePath);
                if (hidden)
                {
                    fileInfo.Attributes |= FileAttributes.Hidden;
                }
                else
                {
                    fileInfo.Attributes &= ~FileAttributes.Hidden;
                }

                // Verify it was set
                fileInfo.Refresh();
                return fileInfo.Attributes.HasFlag(FileAttributes.Hidden) == hidden;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error: {ex.Message}. Make sure 'fatattr' is installed (sudo apt install fatattr)", ex);
        }

        return false;
    }
}
