namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Rumble scaling, in percent of the game's preset values. "Hit" presets are the note cut, bad cut, bomb and chain
/// presets; "other" covers continuous rumble (arcs, obstacles, saber clash) and menu clicks.
/// </summary>
internal sealed record HapticsSettings(int HitStrengthPercent, int OtherStrengthPercent, int DurationPercent)
{
    public static HapticsSettings Default { get; } = new(60, 30, 30);

    public override string ToString() =>
        $"hit {HitStrengthPercent}%, other {OtherStrengthPercent}%, duration {DurationPercent}%";
}
