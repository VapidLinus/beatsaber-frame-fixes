using System.Globalization;
using BeatSaberFrameFixes.Patches;

namespace BeatSaberFrameFixes;

internal enum Command
{
    Apply,
    Restore,
    Help,
    Version,
}

/// <summary>
/// How the bloom blur width changes: <see cref="Off"/> keeps the game's width, <see cref="Full"/> uses one width
/// everywhere, <see cref="Song"/> uses another width while a song plays, and <see cref="Aggressive"/> also narrows it
/// right after note hits.
/// </summary>
internal enum BloomMode
{
    Off,
    Full,
    Song,
    Aggressive,
}

/// <summary>Thrown for invalid command-line arguments; the message is shown to the user.</summary>
internal sealed class OptionsException(string message) : Exception(message);

/// <summary>
/// Parsed command line. A null <see cref="Haptics"/> or <see cref="PauseDebounceMilliseconds"/>, or a false
/// <see cref="BloomSkipCopy"/>, means that fix is turned off (and removed if it was applied before). A null
/// <see cref="BloomBlur"/> keeps the game's bloom blur.
/// </summary>
internal sealed record Options(Command Command, HapticsSettings? Haptics, int? PauseDebounceMilliseconds, bool BloomSkipCopy, BloomBlurSettings? BloomBlur, string? GameDirectory)
{
    public const int DefaultPauseDebounceMilliseconds = 250;
    public const int DefaultBloomSongWidth = 464;
    public const int DefaultBloomBusyWidth = 256;

    public const string Usage = """
        Usage: beatsaber-frame-fixes [options]

        Tweaks the rumble and applies the pause and bloom fixes to Beat Saber.
        Run it again with different options to change them, or with --restore to
        undo everything.
        Switches take on or off (also true/false, yes/no, 1/0).

        Rumble:
          --rumble-tweaks <on|off>     Apply the rumble settings below (default on);
                                       off keeps the game's own rumble
          --hit-strength <percent>     Rumble strength for note hits, bad cuts, bombs
                                       and chains (default 40)
          --other-strength <percent>   Rumble strength for arcs, walls, saber clashes
                                       and menu clicks (default 20)
          --hit-duration <percent>     Rumble length for note hits, bad cuts, bombs
                                       and chains (default 65)
          --other-duration <percent>   Rumble length for menu clicks; arcs, walls and
                                       saber clashes last while touching (default 100)
          --duration <percent>         Sets both lengths at once

        Pausing:
          --pause-fix <on|off>         Ignore brief headset-off blips (default on); off
                                       keeps the game's own pausing
          --pause-debounce <ms>        How long the headset must report focus or
                                       presence lost before the game pauses (default 250)

        Bloom:
          --bloom-skip-copy <on|off>   Skip the bloom's full-screen copy (default on)
          --bloom-mode <mode>          How the bloom blur width changes (default
                                       aggressive). The game's own width is 928;
                                       narrower is cheaper but makes the glow blockier.
                                         off         the game's width everywhere
                                         full        --bloom-width everywhere
                                         song        --bloom-width, and
                                                     --bloom-song-width while a song
                                                     is playing
                                         aggressive  like song, and --bloom-busy-width
                                                     right after note hits
          --bloom-width <px>           Width in menus and pauses, and everywhere in
                                       full mode (default: the game's own)
          --bloom-song-width <px>      Width while a song is playing (default 464)
          --bloom-busy-width <px>      Width right after a note hit (default 256)
          --bloom-busy-delay <s>       Seconds after the last note hit until the song
                                       width comes back (default 1.5)

        Other:
          --restore                    Put the original game files back
          --game-dir <folder>          Beat Saber folder, if it isn't found automatically
          --version                    Show the version
          -h, --help                   Show this help
        """;

    public static Options Parse(string[] args)
    {
        var command = Command.Apply;
        int hit = HapticsSettings.Default.HitStrengthPercent;
        int other = HapticsSettings.Default.OtherStrengthPercent;
        int hitDuration = HapticsSettings.Default.HitDurationPercent;
        int otherDuration = HapticsSettings.Default.OtherDurationPercent;
        int debounce = DefaultPauseDebounceMilliseconds;
        bool rumbleTweaks = true;
        bool pauseFix = true;
        bool skipCopy = true;
        var rumbleSettings = new List<string>();
        var pauseSettings = new List<string>();
        int? bloomWidth = null;
        int? bloomSongWidth = null;
        int? bloomBusyWidth = null;
        var bloomBusySeconds = BloomBlurPatch.DefaultBusySeconds;
        var bloomMode = BloomMode.Aggressive;
        var bloomSettings = new List<string>();
        string? gameDirectory = null;

        for (var i = 0; i < args.Length; i++)
        {
            var (name, inlineValue) = SplitArgument(args[i]);
            string Value() => inlineValue ?? (i + 1 < args.Length ? args[++i] : throw new OptionsException($"{name} needs a value."));
            int RumblePercent()
            {
                rumbleSettings.Add(name);
                return ParseNumber(name, Value(), max: 1000);
            }

            switch (name)
            {
                case "--rumble-tweaks": rumbleTweaks = ParseSwitch(name, Value()); break;
                case "--hit-strength": hit = RumblePercent(); break;
                case "--other-strength": other = RumblePercent(); break;
                case "--hit-duration": hitDuration = RumblePercent(); break;
                case "--other-duration": otherDuration = RumblePercent(); break;
                case "--duration": hitDuration = otherDuration = RumblePercent(); break;
                case "--pause-fix": pauseFix = ParseSwitch(name, Value()); break;
                case "--pause-debounce": pauseSettings.Add(name); debounce = ParseNumber(name, Value(), max: 10000); break;
                case "--bloom-skip-copy": skipCopy = ParseSwitch(name, Value()); break;
                case "--bloom-mode": bloomMode = ParseBloomMode(name, Value()); break;
                case "--bloom-width": bloomSettings.Add(name); bloomWidth = ParseNumber(name, Value(), min: BloomBlurPatch.MinWidth, max: BloomBlurPatch.MaxWidth); break;
                case "--bloom-song-width": bloomSettings.Add(name); bloomSongWidth = ParseNumber(name, Value(), min: BloomBlurPatch.MinWidth, max: BloomBlurPatch.MaxWidth); break;
                case "--bloom-busy-width": bloomSettings.Add(name); bloomBusyWidth = ParseNumber(name, Value(), min: BloomBlurPatch.MinWidth, max: BloomBlurPatch.MaxWidth); break;
                case "--bloom-busy-delay": bloomSettings.Add(name); bloomBusySeconds = ParseSeconds(name, Value(), min: 0.1f, max: 10f); break;
                case "--game-dir": gameDirectory = Value(); break;
                case "--restore": command = Command.Restore; break;
                case "--version": command = Command.Version; break;
                case "-h" or "--help": command = Command.Help; break;
                default: throw new OptionsException($"Unknown option: {args[i]}");
            }
        }

        var bloomBlur = bloomMode switch
        {
            BloomMode.Off => null,
            BloomMode.Full => BloomBlurFor(bloomWidth, songWidth: null, busyWidth: null, bloomBusySeconds),
            BloomMode.Song => BloomBlurFor(bloomWidth, bloomSongWidth ?? DefaultBloomSongWidth, busyWidth: null, bloomBusySeconds),
            _ => BloomBlurFor(bloomWidth, bloomSongWidth ?? DefaultBloomSongWidth, bloomBusyWidth ?? DefaultBloomBusyWidth, bloomBusySeconds),
        };
        if (command == Command.Apply && !rumbleTweaks && !pauseFix && !skipCopy && bloomBlur is null)
            throw new OptionsException("Turning every fix off leaves nothing to do. Use --restore to undo the fixes.");

        return new Options(
            command,
            rumbleTweaks ? new HapticsSettings(hit, other, hitDuration, otherDuration) : null,
            pauseFix ? debounce : null,
            skipCopy,
            bloomBlur,
            gameDirectory)
        {
            Warnings =
            [
                .. IgnoredWarning(rumbleTweaks ? [] : rumbleSettings, "--rumble-tweaks is off"),
                .. IgnoredWarning(pauseFix ? [] : pauseSettings, "--pause-fix is off"),
                .. IgnoredWarning(bloomSettings.Where(setting => !UsedBy(bloomMode, setting)).ToList(), $"--bloom-mode is {bloomMode.ToString().ToLowerInvariant()}"),
            ],
        };
    }

    /// <summary>Things worth telling the user that don't stop the run, such as settings ignored because their switch is off.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    private static IEnumerable<string> IgnoredWarning(List<string> settings, string reason)
    {
        if (settings.Count > 0)
            yield return $"{reason}, so {string.Join(", ", settings.Distinct())} {(settings.Distinct().Count() == 1 ? "is" : "are")} ignored.";
    }

    /// <summary>
    /// The blur settings, or null when they match the game. A song width is only kept when it is narrower than the width
    /// elsewhere, and a busy width only when it is narrower than the width used during songs.
    /// </summary>
    private static BloomBlurSettings? BloomBlurFor(int? width, int? songWidth, int? busyWidth, float busySeconds)
    {
        var outsideSong = width ?? BloomBlurPatch.GameWidth;
        if (songWidth >= outsideSong)
            songWidth = null;
        if (busyWidth >= (songWidth ?? outsideSong))
            busyWidth = null;
        return width is not null || songWidth is not null || busyWidth is not null
            ? new BloomBlurSettings(width, songWidth, busyWidth, busyWidth is null ? BloomBlurPatch.DefaultBusySeconds : busySeconds)
            : null;
    }

    /// <summary>Whether <paramref name="mode"/> uses the bloom setting with the given option name.</summary>
    private static bool UsedBy(BloomMode mode, string setting) => mode switch
    {
        BloomMode.Off => false,
        BloomMode.Full => setting is "--bloom-width",
        BloomMode.Song => setting is "--bloom-width" or "--bloom-song-width",
        _ => true,
    };

    private static BloomMode ParseBloomMode(string name, string value) => value.ToLowerInvariant() switch
    {
        "off" => BloomMode.Off,
        "full" => BloomMode.Full,
        "song" => BloomMode.Song,
        "aggressive" => BloomMode.Aggressive,
        _ => throw new OptionsException($"{name} must be off, full, song or aggressive, not \"{value}\"."),
    };

    private static float ParseSeconds(string name, string value, float min, float max)
    {
        if (!float.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds) || seconds < min || seconds > max)
            throw new OptionsException($"{name} must be a number of seconds from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}, not \"{value}\".");
        return seconds;
    }

    private static (string Name, string? Value) SplitArgument(string argument)
    {
        var equals = argument.IndexOf('=');
        return argument.StartsWith("--") && equals > 0 ? (argument[..equals], argument[(equals + 1)..]) : (argument, null);
    }

    private static int ParseNumber(string name, string value, int max, int min = 0)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < min || number > max)
            throw new OptionsException($"{name} must be a whole number from {min} to {max}, not \"{value}\".");
        return number;
    }

    private static bool ParseSwitch(string name, string value) => value.ToLowerInvariant() switch
    {
        "on" or "true" or "yes" or "1" => true,
        "off" or "false" or "no" or "0" => false,
        _ => throw new OptionsException($"{name} must be on or off, not \"{value}\"."),
    };
}
