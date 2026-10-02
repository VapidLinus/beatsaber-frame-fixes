using System.Security.Cryptography;
using BeatSaberFrameFixes.Patches;
using Mono.Cecil;

namespace BeatSaberFrameFixes;

/// <summary>Thrown when the install cannot proceed; the message is shown to the user and says what to do next.</summary>
internal sealed class InstallerException(string message) : Exception(message);

/// <summary>
/// Applies, changes or removes the fixes in a game install. Original files are backed up under
/// <paramref name="backupRoot"/>, named by their SHA-256, which each patched file records in its
/// <see cref="PatchMarker"/>. Patches are always applied to an original, never on top of an earlier patch, and every
/// file is patched in memory first: game files are only replaced once all patches succeeded.
/// </summary>
internal sealed class Installer(GameInstall game, string backupRoot, TextWriter output)
{
    public const string RepositoryUrl = "https://github.com/VapidLinus/beatsaber-frame-fixes";

    private const string VerifyFilesAdvice =
        "Restore the game files with Steam (Beat Saber > Properties > Installed Files > Verify integrity of game files) and run this again.";

    private static readonly string[] PatchedFiles = [HapticsPatch.FileName, PauseDebouncePatch.FileName];

    /// <summary>Backup file name for an original game file with the given SHA-256, e.g. <c>Main-1f3a….dll</c>.</summary>
    public static string BackupFileName(string fileName, string sha256) =>
        $"{Path.GetFileNameWithoutExtension(fileName)}-{sha256}{Path.GetExtension(fileName)}";

    public void Apply(HapticsSettings? haptics, int? pauseDebounceMilliseconds)
    {
        var fixes = new[]
        {
            new Fix(HapticsPatch.FileName, "Rumble fix",
                haptics is null ? null : m => HapticsPatch.Apply(m, haptics),
                $"hits {haptics?.HitStrengthPercent}% strength and {haptics?.HitDurationPercent}% length, other rumble {haptics?.OtherStrengthPercent}% and {haptics?.OtherDurationPercent}%",
                $"rumble {haptics}"),
            new Fix(PauseDebouncePatch.FileName, "Pause fix",
                pauseDebounceMilliseconds is not { } ms ? null : m => PauseDebouncePatch.Apply(m, ms),
                $"pauses only after focus or presence is lost for {pauseDebounceMilliseconds} ms",
                $"pause debounce {pauseDebounceMilliseconds} ms"),
        };

        var changes = fixes.Select(Prepare).OfType<FileChange>().ToList();
        BackUp(changes);
        Replace(changes);
        foreach (var change in changes)
            output.WriteLine(change.Message);
        if (changes.Count == 0)
            output.WriteLine("Nothing to change.");
    }

    public void Restore()
    {
        var changes = new List<FileChange>();
        foreach (var fileName in PatchedFiles)
        {
            if (ReadMarker(File.ReadAllBytes(GamePath(fileName))) is { } marker)
                changes.Add(new FileChange(fileName, ReadBackup(fileName, marker), OriginalToBackUp: null, $"Restored the original {fileName}"));
        }
        if (changes.Count == 0)
        {
            output.WriteLine("Beat Saber is already using its original files. Nothing to restore.");
            return;
        }
        Replace(changes);
        foreach (var change in changes)
            output.WriteLine(change.Message);
    }

    private FileChange? Prepare(Fix fix)
    {
        var current = File.ReadAllBytes(GamePath(fix.FileName));
        var marker = ReadMarker(current);
        if (fix.Patch is null)
            return marker is null ? null : new FileChange(fix.FileName, ReadBackup(fix.FileName, marker), OriginalToBackUp: null, $"{fix.Name} removed");

        var original = marker is null ? current : ReadBackup(fix.FileName, marker);
        var info = new PatchMarker.Info($"beatsaber-frame-fixes {Program.Version}: {fix.MarkerDescription}", Sha256(original));
        return new FileChange(fix.FileName, Patch(original, fix, info), marker is null ? original : null, $"{fix.Name} applied: {fix.Summary}");
    }

    private byte[] Patch(byte[] original, Fix fix, PatchMarker.Info info)
    {
        using var module = ReadModule(original);
        try
        {
            fix.Patch!(module);
        }
        catch (PatchTargetMismatchException e)
        {
            throw new InstallerException(
                $"Beat Saber {game.DisplayVersion} has changed the code the {fix.Name.ToLowerInvariant()} relies on ({e.Message}). " +
                $"Nothing was changed. Check {RepositoryUrl} for an update.");
        }
        PatchMarker.Add(module, info);
        var patched = new MemoryStream();
        module.Write(patched);
        return patched.ToArray();
    }

    private void BackUp(IEnumerable<FileChange> changes)
    {
        var backedUp = false;
        foreach (var change in changes)
        {
            if (change.OriginalToBackUp is not { } original)
                continue;
            Directory.CreateDirectory(backupRoot);
            var path = Path.Combine(backupRoot, BackupFileName(change.FileName, Sha256(original)));
            if (!File.Exists(path))
                File.WriteAllBytes(path, original);
            backedUp = true;
        }
        if (backedUp)
            output.WriteLine($"Backed up the original game files to {backupRoot}");
    }

    private void Replace(IReadOnlyList<FileChange> changes)
    {
        var temporaryFiles = changes.Select(c => GamePath(c.FileName) + ".bsff.tmp").ToList();
        try
        {
            for (var i = 0; i < changes.Count; i++)
                File.WriteAllBytes(temporaryFiles[i], changes[i].Contents);
            for (var i = 0; i < changes.Count; i++)
                File.Move(temporaryFiles[i], GamePath(changes[i].FileName), overwrite: true);
        }
        finally
        {
            foreach (var file in temporaryFiles.Where(File.Exists))
                File.Delete(file);
        }
    }

    private byte[] ReadBackup(string fileName, PatchMarker.Info marker)
    {
        var path = Path.Combine(backupRoot, BackupFileName(fileName, marker.OriginalSha256));
        var backup = File.Exists(path) ? File.ReadAllBytes(path) : null;
        if (backup is null || Sha256(backup) != marker.OriginalSha256)
            throw new InstallerException($"{fileName} is already patched, but the backup of the original is missing. {VerifyFilesAdvice}");
        return backup;
    }

    private PatchMarker.Info? ReadMarker(byte[] assembly)
    {
        using var module = ReadModule(assembly);
        return PatchMarker.Read(module);
    }

    private ModuleDefinition ReadModule(byte[] assembly)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(game.ManagedDirectory);
        return ModuleDefinition.ReadModule(new MemoryStream(assembly), new ReaderParameters { AssemblyResolver = resolver });
    }

    private string GamePath(string fileName) => Path.Combine(game.ManagedDirectory, fileName);

    private static string Sha256(byte[] contents) => Convert.ToHexStringLower(SHA256.HashData(contents));

    /// <summary>One fix and the game file it patches. A null <see cref="Patch"/> means the fix is skipped.</summary>
    private sealed record Fix(string FileName, string Name, Action<ModuleDefinition>? Patch, string Summary, string MarkerDescription);

    /// <summary>New contents for a game file, and the unpatched original to back up first when the file is not patched yet.</summary>
    private sealed record FileChange(string FileName, byte[] Contents, byte[]? OriginalToBackUp, string Message);
}
