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

/// <summary>Thrown for invalid command-line arguments; the message is shown to the user.</summary>
internal sealed class OptionsException(string message) : Exception(message);

/// <summary>
/// Parsed command line. A null <see cref="Haptics"/> or <see cref="PauseDebounceMilliseconds"/>, or a false
/// <see cref="BloomFix"/>, means that fix is skipped (and removed if it was applied before). A null <see cref="BloomWidth"/>
/// keeps the game's bloom blur.
/// </summary>
internal sealed record Options(Command Command, HapticsSettings? Haptics, int? PauseDebounceMilliseconds, bool BloomFix, int? BloomWidth, string? GameDirectory)
{
    public const int DefaultPauseDebounceMilliseconds = 250;

    public const string Usage = """
        Usage: beatsaber-frame-fixes [options]

        Applies the rumble, pause and bloom fixes to Beat Saber. Run it again with
        different options to change them, or with --restore to undo everything.

        Options:
          --hit-strength <percent>     Rumble strength for note hits, bad cuts, bombs
                                       and chains (default 60)
          --other-strength <percent>   Rumble strength for arcs, walls, saber clashes
                                       and menu clicks (default 30)
          --hit-duration <percent>     Rumble length for note hits, bad cuts, bombs
                                       and chains (default 60)
          --other-duration <percent>   Rumble length for menu clicks; arcs, walls and
                                       saber clashes last while touching (default 780)
          --duration <percent>         Sets both lengths at once
          --pause-debounce <ms>        How long the headset must report focus or
                                       presence lost before the game pauses (default 250)
          --no-haptics                 Leave rumble as the game has it
          --no-pause-fix               Leave pausing as the game has it
          --no-bloom-fix               Leave the bloom effect as the game has it
          --bloom-width <px>           Width of the bloom blur texture (game: 512). Narrower
                                       is cheaper but makes the glow blockier
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
        bool haptics = true;
        bool pauseFix = true;
        bool bloomFix = true;
        int? bloomWidth = null;
        string? gameDirectory = null;

        for (var i = 0; i < args.Length; i++)
        {
            var (name, inlineValue) = SplitArgument(args[i]);
            string Value() => inlineValue ?? (i + 1 < args.Length ? args[++i] : throw new OptionsException($"{name} needs a value."));

            switch (name)
            {
                case "--hit-strength": hit = ParseNumber(name, Value(), max: 1000); break;
                case "--other-strength": other = ParseNumber(name, Value(), max: 1000); break;
                case "--hit-duration": hitDuration = ParseNumber(name, Value(), max: 1000); break;
                case "--other-duration": otherDuration = ParseNumber(name, Value(), max: 1000); break;
                case "--duration": hitDuration = otherDuration = ParseNumber(name, Value(), max: 1000); break;
                case "--pause-debounce": debounce = ParseNumber(name, Value(), max: 10000); break;
                case "--no-haptics": haptics = false; break;
                case "--no-pause-fix": pauseFix = false; break;
                case "--no-bloom-fix": bloomFix = false; break;
                case "--bloom-width": bloomWidth = ParseNumber(name, Value(), min: BloomBlurPatch.MinWidth, max: BloomBlurPatch.MaxWidth); break;
                case "--game-dir": gameDirectory = Value(); break;
                case "--restore": command = Command.Restore; break;
                case "--version": command = Command.Version; break;
                case "-h" or "--help": command = Command.Help; break;
                default: throw new OptionsException($"Unknown option: {args[i]}");
            }
        }

        if (command == Command.Apply && !haptics && !pauseFix && !bloomFix && bloomWidth is null)
            throw new OptionsException("Skipping every fix leaves nothing to do. Use --restore to undo the fixes.");

        return new Options(
            command,
            haptics ? new HapticsSettings(hit, other, hitDuration, otherDuration) : null,
            pauseFix ? debounce : null,
            bloomFix,
            bloomWidth,
            gameDirectory);
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
}
