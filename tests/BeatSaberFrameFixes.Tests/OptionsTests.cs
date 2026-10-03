using BeatSaberFrameFixes.Patches;

namespace BeatSaberFrameFixes.Tests;

public class OptionsTests
{
    [Fact]
    public void No_arguments_apply_everything_with_the_defaults()
    {
        var options = Options.Parse([]);

        Assert.Equal(new HapticsSettings(60, 30, 60, 780), options.Haptics);
        Assert.Equal(250, options.PauseDebounceMilliseconds);
        Assert.True(options.BloomSkipCopy);
        Assert.Equal(new BloomBlurSettings(null, SongWidth: 256), options.BloomBlur);
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
    public void Each_change_can_be_switched_off()
    {
        Assert.Null(Options.Parse(["--rumble-tweaks", "off"]).Haptics);
        Assert.Null(Options.Parse(["--pause-fix", "off"]).PauseDebounceMilliseconds);
        Assert.False(Options.Parse(["--bloom-skip-copy", "off"]).BloomSkipCopy);
    }

    [Theory]
    [InlineData("on", true)]
    [InlineData("ON", true)]
    [InlineData("true", true)]
    [InlineData("yes", true)]
    [InlineData("1", true)]
    [InlineData("off", false)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    [InlineData("0", false)]
    public void Switches_accept_common_words(string value, bool expected)
    {
        Assert.Equal(expected, Options.Parse(["--bloom-skip-copy", value]).BloomSkipCopy);
        Assert.Equal(expected, Options.Parse([$"--bloom-skip-copy={value}"]).BloomSkipCopy);
    }

    [Theory]
    [InlineData("--hit-strength", "80")]
    [InlineData("--duration", "45")]
    public void Rumble_settings_with_rumble_tweaks_off_are_ignored_with_a_warning(string setting, string value)
    {
        var options = Options.Parse(["--rumble-tweaks", "off", setting, value]);

        Assert.Null(options.Haptics);
        Assert.Equal([$"--rumble-tweaks is off, so {setting} is ignored."], options.Warnings);
    }

    [Fact]
    public void Several_ignored_settings_share_one_warning()
    {
        var options = Options.Parse(["--hit-strength", "80", "--duration", "45", "--hit-strength", "70", "--rumble-tweaks", "off"]);

        Assert.Equal(["--rumble-tweaks is off, so --hit-strength, --duration are ignored."], options.Warnings);
    }

    [Fact]
    public void Pause_debounce_with_the_pause_fix_off_is_ignored_with_a_warning()
    {
        var options = Options.Parse(["--pause-debounce", "500", "--pause-fix", "off"]);

        Assert.Null(options.PauseDebounceMilliseconds);
        Assert.Equal(["--pause-fix is off, so --pause-debounce is ignored."], options.Warnings);
    }

    [Fact]
    public void Settings_with_their_switch_on_give_no_warning()
    {
        Assert.Empty(Options.Parse(["--hit-strength", "80", "--pause-debounce", "500"]).Warnings);
    }

    [Fact]
    public void Bloom_blur_widths_can_be_set()
    {
        Assert.Equal(new BloomBlurSettings(null, SongWidth: 384), Options.Parse(["--bloom-song-width", "384"]).BloomBlur);
        Assert.Equal(new BloomBlurSettings(384, SongWidth: 256), Options.Parse(["--bloom-width", "384"]).BloomBlur);
    }

    [Fact]
    public void Song_width_of_512_keeps_the_game_blur()
    {
        Assert.Null(Options.Parse(["--bloom-song-width", "512"]).BloomBlur);
    }

    [Fact]
    public void Song_width_never_widens_the_blur()
    {
        Assert.Equal(new BloomBlurSettings(256, SongWidth: null), Options.Parse(["--bloom-width", "256"]).BloomBlur);
        Assert.Equal(new BloomBlurSettings(128, SongWidth: null), Options.Parse(["--bloom-width", "128"]).BloomBlur);
    }

    [Fact]
    public void Bloom_copy_and_blur_are_independent()
    {
        var options = Options.Parse(["--bloom-skip-copy", "off"]);

        Assert.False(options.BloomSkipCopy);
        Assert.Equal(new BloomBlurSettings(null, SongWidth: 256), options.BloomBlur);
    }

    [Fact]
    public void Turning_everything_off_is_rejected()
    {
        Assert.Throws<OptionsException>(() => Options.Parse(
            ["--rumble-tweaks", "off", "--pause-fix", "off", "--bloom-skip-copy", "off", "--bloom-song-width", "512"]));
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
    [InlineData("--rumble-tweaks", "maybe")]
    [InlineData("--rumble-tweaks")]
    [InlineData("--no-haptics")]
    [InlineData("--no-pause-fix")]
    [InlineData("--no-bloom-fix")]
    [InlineData("--unknown")]
    [InlineData("--hit-strength")]
    public void Invalid_arguments_are_rejected(params string[] args)
    {
        Assert.Throws<OptionsException>(() => Options.Parse(args));
    }
}
