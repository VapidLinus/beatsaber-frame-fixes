using BeatSaberFrameFixes.Patches;
using Mono.Cecil;

namespace BeatSaberFrameFixes.Tests;

public sealed class InstallerTests : IDisposable
{
    private static readonly HapticsSettings DefaultHaptics = new(40, 20, 65, 100);

    private readonly string _root = Directory.CreateTempSubdirectory("bsff-tests-").FullName;
    private readonly StringWriter _output = new();
    private readonly GameInstall _game;

    public InstallerTests()
    {
        _game = CreateFakeGame("1.45.1_27839");
    }

    private string BackupRoot => Path.Combine(_root, "backup");

    private string ManagedFile(string name) => Path.Combine(_game.ManagedDirectory, name);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Apply_patches_every_file_and_backs_up_the_originals()
    {
        var originalMain = File.ReadAllBytes(ManagedFile("Main.dll"));
        var originalHaptics = File.ReadAllBytes(ManagedFile("BeatSaber.Haptics.dll"));
        var originalRendering = File.ReadAllBytes(ManagedFile("Rendering.dll"));

        CreateInstaller().Apply(DefaultHaptics, pauseDebounceMilliseconds: 250, bloomSkipCopy: true);

        Assert.Contains("hit 40%", ReadMarker("BeatSaber.Haptics.dll"));
        Assert.Contains("250 ms", ReadMarker("Main.dll"));
        Assert.Contains("bloom", ReadMarker("Rendering.dll"));
        Assert.Equal(originalMain, File.ReadAllBytes(BackupFile("Main.dll", originalMain)));
        Assert.Equal(originalHaptics, File.ReadAllBytes(BackupFile("BeatSaber.Haptics.dll", originalHaptics)));
        Assert.Equal(originalRendering, File.ReadAllBytes(BackupFile("Rendering.dll", originalRendering)));
    }

    [Fact]
    public void Song_bloom_width_adds_the_song_flag_to_main_even_without_the_pause_fix()
    {
        CreateInstaller().Apply(DefaultHaptics, pauseDebounceMilliseconds: null, bloomSkipCopy: true, new BloomBlurSettings(Width: null, SongWidth: 256));

        Assert.Equal("beatsaber-frame-fixes " + Program.Version + ": song playing flag", ReadMarker("Main.dll"));
        Assert.Contains("256 px wide while a song plays", ReadMarker("Rendering.dll"));
    }

    [Fact]
    public void Busy_bloom_width_adds_the_note_hit_time_to_main()
    {
        CreateInstaller().Apply(DefaultHaptics, pauseDebounceMilliseconds: null, bloomSkipCopy: true, new BloomBlurSettings(Width: null, SongWidth: 464, BusyWidth: 256));

        Assert.Equal("beatsaber-frame-fixes " + Program.Version + ": song playing flag, note hit time", ReadMarker("Main.dll"));
        Assert.Contains("256 px for 1.5 s after a note hit", ReadMarker("Rendering.dll"));
        Assert.Contains("tells the bloom when a song is playing and when notes are hit", _output.ToString());
    }

    [Fact]
    public void Skipping_the_bloom_fix_restores_rendering()
    {
        var originalRendering = File.ReadAllBytes(ManagedFile("Rendering.dll"));
        CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: true);

        CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: false);

        Assert.Equal(originalRendering, File.ReadAllBytes(ManagedFile("Rendering.dll")));
        Assert.Contains("Bloom fix removed", _output.ToString());
    }

    [Fact]
    public void Reapplying_patches_from_the_backup_instead_of_stacking()
    {
        CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: true);

        CreateInstaller().Apply(new HapticsSettings(80, 40, 50, 100), 500, bloomSkipCopy: true);

        Assert.Contains("hit 80%", ReadMarker("BeatSaber.Haptics.dll"));
        Assert.Contains("500 ms", ReadMarker("Main.dll"));
        using var main = StandIns.ReadModule(ManagedFile("Main.dll"));
        Assert.Single(main.GetType("PauseController").Methods, m => m.Name == "Update");
    }

    [Fact]
    public void Skipping_a_fix_restores_that_file()
    {
        var originalHaptics = File.ReadAllBytes(ManagedFile("BeatSaber.Haptics.dll"));
        CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: true);

        CreateInstaller().Apply(haptics: null, pauseDebounceMilliseconds: 250, bloomSkipCopy: true);

        Assert.Equal(originalHaptics, File.ReadAllBytes(ManagedFile("BeatSaber.Haptics.dll")));
        Assert.NotNull(ReadMarker("Main.dll"));
    }

    [Fact]
    public void Restore_puts_the_originals_back()
    {
        var originalMain = File.ReadAllBytes(ManagedFile("Main.dll"));
        var originalHaptics = File.ReadAllBytes(ManagedFile("BeatSaber.Haptics.dll"));
        CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: true);

        CreateInstaller().Restore();

        Assert.Equal(originalMain, File.ReadAllBytes(ManagedFile("Main.dll")));
        Assert.Equal(originalHaptics, File.ReadAllBytes(ManagedFile("BeatSaber.Haptics.dll")));
    }

    [Fact]
    public void Restore_of_an_unpatched_game_changes_nothing()
    {
        var originalMain = File.ReadAllBytes(ManagedFile("Main.dll"));

        CreateInstaller().Restore();

        Assert.Equal(originalMain, File.ReadAllBytes(ManagedFile("Main.dll")));
        Assert.Contains("already", _output.ToString());
    }

    [Fact]
    public void A_mismatch_in_one_fix_leaves_every_file_untouched()
    {
        BreakPauseController();
        var mainBefore = File.ReadAllBytes(ManagedFile("Main.dll"));
        var hapticsBefore = File.ReadAllBytes(ManagedFile("BeatSaber.Haptics.dll"));

        var error = Assert.Throws<InstallerException>(() => CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: true));

        Assert.Contains("Nothing was changed", error.Message);
        Assert.Equal(mainBefore, File.ReadAllBytes(ManagedFile("Main.dll")));
        Assert.Equal(hapticsBefore, File.ReadAllBytes(ManagedFile("BeatSaber.Haptics.dll")));
        Assert.Empty(Directory.GetFiles(_game.ManagedDirectory, "*.tmp"));
    }

    [Fact]
    public void A_patched_file_without_a_backup_is_refused()
    {
        CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: true);
        Directory.Delete(BackupRoot, recursive: true);

        var error = Assert.Throws<InstallerException>(() => CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: true));

        Assert.Contains("Verify integrity", error.Message);
    }

    [Fact]
    public void A_game_update_backs_up_and_restores_the_new_originals()
    {
        CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: true);
        var updatedMain = SimulateGameUpdate("Main.dll");

        CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: true);
        Assert.NotNull(ReadMarker("Main.dll"));
        CreateInstaller().Restore();

        Assert.Equal(updatedMain, File.ReadAllBytes(ManagedFile("Main.dll")));
    }

    [Fact]
    public void A_changed_version_file_does_not_affect_restoring()
    {
        var originalMain = File.ReadAllBytes(ManagedFile("Main.dll"));
        CreateInstaller().Apply(DefaultHaptics, 250, bloomSkipCopy: true);

        new Installer(_game with { Version = "1.45.2_28848" }, BackupRoot, _output).Restore();

        Assert.Equal(originalMain, File.ReadAllBytes(ManagedFile("Main.dll")));
    }

    private Installer CreateInstaller() => new(_game, BackupRoot, _output);

    private string BackupFile(string name, byte[] original) =>
        Path.Combine(BackupRoot, Installer.BackupFileName(name, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(original))));

    private string? ReadMarker(string name)
    {
        using var module = StandIns.ReadModule(ManagedFile(name));
        return PatchMarker.Read(module)?.Description;
    }

    /// <summary>Replaces a game file with a different unpatched build of it, as a Steam update would.</summary>
    private byte[] SimulateGameUpdate(string name)
    {
        var stream = new MemoryStream();
        using (var module = StandIns.ReadModule(StandIns.MainPath))
        {
            module.Types.Add(new TypeDefinition("", "AddedByUpdate", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object));
            module.Write(stream);
        }
        File.WriteAllBytes(ManagedFile(name), stream.ToArray());
        return stream.ToArray();
    }

    private void BreakPauseController()
    {
        using (var module = StandIns.ReadModule(ManagedFile("Main.dll")))
        {
            var controller = module.GetType("PauseController");
            controller.Methods.Remove(controller.Methods.Single(m => m.Name == "HandleSystemStateChange"));
            module.Write(ManagedFile("Main.dll.broken"));
        }
        File.Move(ManagedFile("Main.dll.broken"), ManagedFile("Main.dll"), overwrite: true);
    }

    private GameInstall CreateFakeGame(string version)
    {
        var directory = Path.Combine(_root, "Beat Saber");
        var game = new GameInstall(directory, version);
        Directory.CreateDirectory(game.ManagedDirectory);
        File.WriteAllText(Path.Combine(directory, "BeatSaberVersion.txt"), version);
        foreach (var path in StandIns.AllPaths)
            File.Copy(path, Path.Combine(game.ManagedDirectory, Path.GetFileName(path)));
        return game;
    }
}
