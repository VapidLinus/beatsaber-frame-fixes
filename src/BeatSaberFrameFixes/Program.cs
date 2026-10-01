using System.Reflection;

namespace BeatSaberFrameFixes;

internal static class Program
{
    public static string Version { get; } =
        typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";

    private static int Main(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            switch (options.Command)
            {
                case Command.Help:
                    Console.WriteLine(Options.Usage);
                    return 0;
                case Command.Version:
                    Console.WriteLine($"beatsaber-frame-fixes {Version}");
                    return 0;
            }

            var game = FindGame(options.GameDirectory);
            Console.WriteLine($"Found Beat Saber {game.DisplayVersion} at {game.Directory}");
            if (GameProcess.IsRunning())
                throw new InstallerException("Beat Saber is running. Close it and run this again.");

            var installer = new Installer(game, BackupRoot(), Console.Out);
            if (options.Command == Command.Restore)
                installer.Restore();
            else
                installer.Apply(options.Haptics, options.PauseDebounceMilliseconds);

            Console.WriteLine("Done! You can start Beat Saber now.");
            return 0;
        }
        catch (OptionsException e)
        {
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine("Run with --help to see the options.");
            return 2;
        }
        catch (InstallerException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    private static GameInstall FindGame(string? gameDirectory)
    {
        if (gameDirectory is not null)
            return GameLocator.FromDirectory(gameDirectory)
                ?? throw new InstallerException($"No Beat Saber install found in {gameDirectory}.");
        return GameLocator.Find(HomeDirectory())
            ?? throw new InstallerException("Couldn't find Beat Saber. Make sure it is installed through Steam, or pass its folder with --game-dir.");
    }

    private static string BackupRoot()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrEmpty(dataHome))
            dataHome = Path.Combine(HomeDirectory(), ".local", "share");
        return Path.Combine(dataHome, "beatsaber-frame-fixes", "backup");
    }

    private static string HomeDirectory() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
