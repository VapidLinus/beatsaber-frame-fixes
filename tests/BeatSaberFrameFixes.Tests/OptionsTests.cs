using BeatSaberFrameFixes.Patches;

namespace BeatSaberFrameFixes.Tests;

public class OptionsTests
{
    [Fact]
    public void No_arguments_apply_both_fixes_with_the_defaults()
    {
        var options = Options.Parse([]);

        Assert.Equal(new HapticsSettings(60, 30, 30), options.Haptics);
        Assert.Equal(250, options.PauseDebounceMilliseconds);
        Assert.Equal(Command.Apply, options.Command);
    }

    [Fact]
    public void Values_can_be_overridden()
    {
        var options = Options.Parse(["--hit-strength", "80", "--other-strength=40", "--duration", "100", "--pause-debounce", "500"]);

        Assert.Equal(new HapticsSettings(80, 40, 100), options.Haptics);
        Assert.Equal(500, options.PauseDebounceMilliseconds);
    }

    [Fact]
    public void Each_fix_can_be_skipped()
    {
        Assert.Null(Options.Parse(["--no-haptics"]).Haptics);
        Assert.Null(Options.Parse(["--no-pause-fix"]).PauseDebounceMilliseconds);
    }

    [Fact]
    public void Skipping_both_fixes_is_rejected()
    {
        Assert.Throws<OptionsException>(() => Options.Parse(["--no-haptics", "--no-pause-fix"]));
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
    [InlineData("--unknown")]
    [InlineData("--hit-strength")]
    public void Invalid_arguments_are_rejected(params string[] args)
    {
        Assert.Throws<OptionsException>(() => Options.Parse(args));
    }
}
