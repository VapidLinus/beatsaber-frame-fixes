using BeatSaberFrameFixes.Patches;

namespace BeatSaberFrameFixes.Tests;

public class OptionsTests
{
    [Fact]
    public void No_arguments_apply_both_fixes_with_the_defaults()
    {
        var options = Options.Parse([]);

        Assert.Equal(new HapticsSettings(60, 30, 60, 780), options.Haptics);
        Assert.Equal(250, options.PauseDebounceMilliseconds);
        Assert.True(options.BloomFix);
        Assert.Equal(Command.Apply, options.Command);
    }

    [Fact]
    public void Values_can_be_overridden()
    {
        var options = Options.Parse(["--hit-strength", "80", "--other-strength=40", "--hit-duration", "100", "--other-duration", "10", "--pause-debounce", "500"]);

        Assert.Equal(new HapticsSettings(80, 40, 100, 10), options.Haptics);
        Assert.Equal(500, options.PauseDebounceMilliseconds);
    }

    [Fact]
    public void Duration_sets_both_durations()
    {
        Assert.Equal(new HapticsSettings(60, 30, 45, 45), Options.Parse(["--duration", "45"]).Haptics);
    }

    [Fact]
    public void Each_fix_can_be_skipped()
    {
        Assert.Null(Options.Parse(["--no-haptics"]).Haptics);
        Assert.Null(Options.Parse(["--no-pause-fix"]).PauseDebounceMilliseconds);
        Assert.False(Options.Parse(["--no-bloom-fix"]).BloomFix);
    }

    [Fact]
    public void Bloom_width_is_unchanged_unless_given()
    {
        Assert.Null(Options.Parse([]).BloomWidth);
        Assert.Equal(256, Options.Parse(["--bloom-width", "256"]).BloomWidth);
    }

    [Fact]
    public void Skipping_every_fix_is_rejected()
    {
        Assert.Throws<OptionsException>(() => Options.Parse(["--no-haptics", "--no-pause-fix", "--no-bloom-fix"]));
    }

    [Theory]
    [InlineData("--restore", "Restore")]
    [InlineData("--help", "Help")]
    [InlineData("-h", "Help")]
    [InlineData("--version", "Version")]
    public void Commands_are_recognized(string argument, string expected)
    {
        Assert.Equal(expected, Options.Parse([argument]).Command.ToString());
    }

    [Fact]
    public void Game_directory_can_be_given()
    {
        Assert.Equal("/games/Beat Saber", Options.Parse(["--game-dir", "/games/Beat Saber"]).GameDirectory);
    }

    [Theory]
    [InlineData("--hit-strength", "abc")]
    [InlineData("--hit-strength", "-1")]
    [InlineData("--duration", "1001")]
    [InlineData("--pause-debounce", "10001")]
    [InlineData("--bloom-width", "8")]
    [InlineData("--bloom-width", "4096")]
    [InlineData("--unknown")]
    [InlineData("--hit-strength")]
    public void Invalid_arguments_are_rejected(params string[] args)
    {
        Assert.Throws<OptionsException>(() => Options.Parse(args));
    }
}
