namespace BeatSaberFrameFixes.Tests;

public sealed class GameLocatorTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("bsff-home-").FullName;

    public void Dispose() => Directory.Delete(_home, recursive: true);

    [Fact]
    public void Finds_the_game_in_a_secondary_library_listed_in_libraryfolders()
    {
        var steam = Path.Combine(_home, ".local", "share", "Steam");
        var library = Path.Combine(_home, "games", "SteamLibrary");
        WriteLibraryFolders(steam, steam, library);
        var gameDirectory = InstallGame(library, "Beat Saber", "1.45.1_27839");

        var game = GameLocator.Find(_home);

        Assert.NotNull(game);
        Assert.Equal(gameDirectory, game.Directory);
        Assert.Equal("1.45.1_27839", game.Version);
    }

    [Fact]
    public void Finds_the_game_in_a_flatpak_steam_install()
    {
        var steam = Path.Combine(_home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam");
        WriteLibraryFolders(steam, steam);
        InstallGame(steam, "Beat Saber", "1.45.1_27839");

        Assert.NotNull(GameLocator.Find(_home));
    }

    [Fact]
    public void Returns_null_when_the_game_is_not_installed()
    {
        var steam = Path.Combine(_home, ".local", "share", "Steam");
        WriteLibraryFolders(steam, steam);

        Assert.Null(GameLocator.Find(_home));
    }

    [Fact]
    public void FromDirectory_rejects_a_folder_without_the_game_files()
    {
        Assert.Null(GameLocator.FromDirectory(_home));
    }

    private static void WriteLibraryFolders(string steam, params string[] libraries)
    {
        var steamapps = Directory.CreateDirectory(Path.Combine(steam, "steamapps")).FullName;
        var entries = libraries.Select((path, i) => $$"""
            	"{{i}}"
            	{
            		"path"		"{{path.Replace("\\", "\\\\")}}"
            		"apps"
            		{
            		}
            	}
            """);
        File.WriteAllText(Path.Combine(steamapps, "libraryfolders.vdf"), $"\"libraryfolders\"\n{{\n{string.Join("\n", entries)}\n}}\n");
    }

    private static string InstallGame(string library, string installDir, string version)
    {
        var steamapps = Directory.CreateDirectory(Path.Combine(library, "steamapps")).FullName;
        File.WriteAllText(Path.Combine(steamapps, "appmanifest_620980.acf"), $$"""
            "AppState"
            {
            	"appid"		"620980"
            	"name"		"Beat Saber"
            	"installdir"		"{{installDir}}"
            }
            """);
        var gameDirectory = Path.Combine(steamapps, "common", installDir);
        Directory.CreateDirectory(Path.Combine(gameDirectory, "Beat Saber_Data", "Managed"));
        File.WriteAllText(Path.Combine(gameDirectory, "BeatSaberVersion.txt"), version + "\n");
        return gameDirectory;
    }
}
