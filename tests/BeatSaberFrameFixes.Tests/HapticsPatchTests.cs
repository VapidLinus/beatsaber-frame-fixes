using System.Reflection;
using BeatSaberFrameFixes.Patches;
using Mono.Cecil;

namespace BeatSaberFrameFixes.Tests;

[Collection(nameof(UnityStaticState))]
public class HapticsPatchTests
{
    private static readonly HapticsSettings Settings = new(HitStrengthPercent: 60, OtherStrengthPercent: 30, HitDurationPercent: 50, OtherDurationPercent: 780);

    [Fact]
    public void Hit_presets_use_the_hit_strength_and_the_scaled_duration()
    {
        var rumble = Play(PatchedPlayer(), presetName: "HitNoteHapticPreset", strength: 1f, duration: 0.13f, time: 10f);

        Assert.Equal(0.6f, rumble.Strength, 4);
        Assert.Equal(10f + 0.065f, rumble.EndTime, 4);
    }

    [Theory]
    [InlineData("ClickHapticPreset")]
    [InlineData("ArcSaberHapticPreset")]
    [InlineData("hitLowercaseIsNotAHit")]
    public void Other_presets_use_the_other_strength_and_duration(string presetName)
    {
        var rumble = Play(PatchedPlayer(), presetName, strength: 0.75f, duration: 0.01f, time: 0f);

        Assert.Equal(0.225f, rumble.Strength, 4);
        Assert.Equal(0.078f, rumble.EndTime, 4);
    }

    [Fact]
    public void CanPlayHapticPreset_is_left_unchanged()
    {
        var module = StandIns.ReadModule(StandIns.HapticsPath);
        var canPlay = module.GetType("BeatSaber.Haptics.RumbleHapticFeedbackPlayer").Methods.Single(m => m.Name == "CanPlayHapticPreset");
        var before = canPlay.Body.Instructions.Select(i => i.ToString()).ToList();

        HapticsPatch.Apply(module, Settings);

        Assert.Equal(before, canPlay.Body.Instructions.Select(i => i.ToString()));
    }

    [Fact]
    public void Missing_target_method_is_reported_without_changing_the_module()
    {
        var module = StandIns.ReadModule(StandIns.HapticsPath);
        var player = module.GetType("BeatSaber.Haptics.RumbleHapticFeedbackPlayer");
        player.Methods.Remove(player.Methods.Single(m => m.Name == "PlayHapticFeedback"));

        Assert.Throws<PatchTargetMismatchException>(() => HapticsPatch.Apply(module, Settings));
    }

    [Fact]
    public void Already_scaled_strength_is_reported_as_a_mismatch()
    {
        var module = StandIns.ReadModule(StandIns.HapticsPath);
        HapticsPatch.Apply(module, Settings);

        Assert.Throws<PatchTargetMismatchException>(() => HapticsPatch.Apply(module, Settings));
    }

    private static Assembly PatchedPlayer() => StandIns.PatchAndLoad(StandIns.HapticsPath, m => HapticsPatch.Apply(m, Settings));

    private static (float Strength, float EndTime) Play(Assembly haptics, string presetName, float strength, float duration, float time)
    {
        UnityEngine.Time.time = time;
        var presetType = haptics.GetType("BeatSaber.Haptics.HapticPresetSO", throwOnError: true)!;
        var preset = Activator.CreateInstance(presetType)!;
        presetType.GetProperty("name")!.SetValue(preset, presetName);
        presetType.GetField("_strength")!.SetValue(preset, strength);
        presetType.GetField("_duration")!.SetValue(preset, duration);

        var playerType = haptics.GetType("BeatSaber.Haptics.RumbleHapticFeedbackPlayer", throwOnError: true)!;
        var player = Activator.CreateInstance(playerType)!;
        playerType.GetMethod("PlayHapticFeedback")!.Invoke(player, [UnityEngine.XR.XRNode.LeftHand, preset]);

        var rumble = playerType.GetProperty("LastRumble")!.GetValue(player)!;
        var rumbleType = rumble.GetType();
        return ((float)rumbleType.GetField("strength")!.GetValue(rumble)!, (float)rumbleType.GetField("endTime")!.GetValue(rumble)!);
    }
}
