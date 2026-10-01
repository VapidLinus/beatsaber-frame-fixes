using System.Text.RegularExpressions;

namespace BeatSaberFrameFixes;

/// <summary>Finds the Beat Saber install through Steam's library list, covering native and Flatpak Steam on Linux.</summary>
internal static partial class GameLocator
{
    private const string AppId = "620980";
    private const string VersionFileName = "BeatSaberVersion.txt";

    /// <summary>Steam data folders relative to the home folder, in the order they are searched.</summary>
    private static readonly string[] SteamRoots =
    [
        ".local/share/Steam",
        ".steam/steam",
        ".steam/root",
        ".var/app/com.valvesoftware.Steam/.local/share/Steam",
        ".var/app/com.valvesoftware.Steam/data/Steam",
    ];

    public static GameInstall? Find(string homeDirectory)
    {
        foreach (var library in LibraryFolders(homeDirectory))
        {
            var manifest = Path.Combine(library, "steamapps", $"appmanifest_{AppId}.acf");
            if (!File.Exists(manifest))
                continue;
            var installDir = InstallDirPattern().Match(File.ReadAllText(manifest));
            if (!installDir.Success)
                continue;
            var game = FromDirectory(Path.Combine(library, "steamapps", "common", Unescape(installDir.Groups[1].Value)));
            if (game is not null)
                return game;
        }
        return null;
    }

    /// <summary>Returns the install in <paramref name="directory"/>, or null if it does not contain Beat Saber's game files.</summary>
    public static GameInstall? FromDirectory(string directory)
    {
        var versionFile = Path.Combine(directory, VersionFileName);
        var game = new GameInstall(directory, "");
        if (!File.Exists(versionFile) || !System.IO.Directory.Exists(game.ManagedDirectory))
            return null;
        return game with { Version = File.ReadAllText(versionFile).Trim() };
    }

    private static IEnumerable<string> LibraryFolders(string homeDirectory)
    {
        var seen = new HashSet<string>();
        foreach (var root in SteamRoots.Select(r => Path.Combine(homeDirectory, r)))
        {
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            var paths = File.Exists(vdf)
                ? LibraryPathPattern().Matches(File.ReadAllText(vdf)).Select(m => Unescape(m.Groups[1].Value)).Prepend(root)
                : [root];
            foreach (var path in paths.Where(seen.Add))
                yield return path;
        }
    }

    private static string Unescape(string vdfValue) => vdfValue.Replace(@"\\", @"\");

    [GeneratedRegex("\"path\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex LibraryPathPattern();

    [GeneratedRegex("\"installdir\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\"")]
    private static partial Regex InstallDirPattern();
}
