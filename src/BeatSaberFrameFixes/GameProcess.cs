namespace BeatSaberFrameFixes;

/// <summary>Detects a running Beat Saber process by scanning <c>/proc</c> command lines. Always false off Linux.</summary>
internal static class GameProcess
{
    private const string ExecutableName = "Beat Saber.exe";

    public static bool IsRunning()
    {
        if (!OperatingSystem.IsLinux())
            return false;
        foreach (var process in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(process), out _))
                continue;
            try
            {
                if (IsGameCommandLine(File.ReadAllText(Path.Combine(process, "cmdline"))))
                    return true;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
        return false;
    }

    /// <summary>
    /// True if any argument of a NUL-separated <c>/proc/[pid]/cmdline</c> is a path to the game executable, as in the
    /// Wine process and Proton's launch wrapper.
    /// </summary>
    public static bool IsGameCommandLine(string commandLine) =>
        commandLine.Split('\0').Any(argument => argument.EndsWith(ExecutableName, StringComparison.OrdinalIgnoreCase));
}
