using System.Reflection;
using BeatSaberFrameFixes.Patches;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace BeatSaberFrameFixes.Tests;

[Collection(nameof(UnityStaticState))]
public class BloomBlurPatchTests
{
    [Fact]
    public void Unpatched_blur_uses_the_serialized_width_and_prefilter()
    {
        var effect = new Effect(StandIns.PatchAndLoad(StandIns.RenderingPath, _ => { }));

        Assert.Equal((928, "Prefilter4"), effect.Frame(songPlaying: false));
    }

    [Fact]
    public void Narrower_width_also_switches_to_the_13_tap_prefilter()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(256, SongWidth: null)));

        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: false));
        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: true));
    }

    [Fact]
    public void Song_width_of_464_uses_the_13_tap_prefilter()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(Width: null, SongWidth: 464)));

        Assert.Equal((464, "Prefilter13"), effect.Frame(songPlaying: true));
        Assert.Equal((928, "Prefilter4"), effect.Frame(songPlaying: false));
    }

    [Theory]
    [InlineData(512)]
    [InlineData(640)]
    [InlineData(928)]
    [InlineData(1024)]
    public void Widths_from_512_up_keep_the_4_tap_prefilter(int width)
    {
        var effect = new Effect(Patch(new BloomBlurSettings(width, SongWidth: null)));

        Assert.Equal((width, "Prefilter4"), effect.Frame(songPlaying: false));
    }

    [Fact]
    public void Song_width_applies_only_while_a_song_plays()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(Width: null, SongWidth: 256)));

        Assert.Equal((928, "Prefilter4"), effect.Frame(songPlaying: false));
        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: true));
        Assert.Equal((928, "Prefilter4"), effect.Frame(songPlaying: false));
    }

    [Fact]
    public void Song_width_and_width_can_be_combined()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(Width: 384, SongWidth: 256)));

        Assert.Equal((384, "Prefilter13"), effect.Frame(songPlaying: false));
        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: true));
    }

    [Fact]
    public void Blur_settings_and_copy_fix_can_be_combined()
    {
        var effect = new Effect(StandIns.PatchAndLoad(StandIns.RenderingPath, m =>
        {
            BloomCopyPatch.Apply(m);
            BloomBlurPatch.Apply(m, new BloomBlurSettings(Width: null, SongWidth: 256));
        }));

        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: true));
    }

    [Fact]
    public void Busy_width_applies_for_the_busy_delay_after_a_note_hit_during_songs()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(Width: null, SongWidth: 464, BusyWidth: 256)));

        Assert.Equal((464, "Prefilter13"), effect.Frame(songPlaying: true));
        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: true, secondsSinceHit: 0.1f));
        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: true, secondsSinceHit: 1.4f));
        Assert.Equal((464, "Prefilter13"), effect.Frame(songPlaying: true, secondsSinceHit: 1.6f));
        Assert.Equal((928, "Prefilter4"), effect.Frame(songPlaying: false, secondsSinceHit: 0.1f));
    }

    [Fact]
    public void Busy_delay_comes_from_the_settings()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(Width: null, SongWidth: 464, BusyWidth: 256, BusySeconds: 0.5f)));

        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: true, secondsSinceHit: 0.4f));
        Assert.Equal((464, "Prefilter13"), effect.Frame(songPlaying: true, secondsSinceHit: 0.6f));
    }

    [Fact]
    public void Busy_width_works_without_a_song_width()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(Width: null, SongWidth: null, BusyWidth: 256)));

        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: true, secondsSinceHit: 0.5f));
        Assert.Equal((928, "Prefilter4"), effect.Frame(songPlaying: true, secondsSinceHit: 5f));
    }

    [Fact]
    public void Width_changes_are_logged()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(Width: null, SongWidth: 464)));
        UnityEngine.Debug.Messages.Clear();

        effect.Frame(songPlaying: false);
        effect.Frame(songPlaying: false);
        effect.Frame(songPlaying: true);
        effect.Frame(songPlaying: true);
        effect.Frame(songPlaying: false);

        Assert.Equal(
            ["Bloom width now 928 px (change 1)", "Bloom width now 464 px (change 2)", "Bloom width now 928 px (change 3)"],
            WidthMessages());
    }

    [Fact]
    public void Flickering_width_logs_the_first_200_changes_only()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(Width: null, SongWidth: 464)));
        UnityEngine.Debug.Messages.Clear();

        for (var frame = 0; frame < 600; frame++)
            effect.Frame(songPlaying: frame % 2 == 0);

        Assert.Equal(BloomBlurPatch.LoggedWidthChanges, WidthMessages().Count);
        Assert.Equal("Bloom width now 928 px (change 200)", WidthMessages()[^1]);
    }

    [Fact]
    public void Patching_twice_is_reported_as_a_mismatch()
    {
        var module = StandIns.ReadModule(StandIns.RenderingPath);
        BloomBlurPatch.Apply(module, new BloomBlurSettings(256, SongWidth: null));

        Assert.Throws<PatchTargetMismatchException>(() => BloomBlurPatch.Apply(module, new BloomBlurSettings(128, SongWidth: null)));
    }

    [Fact]
    public void Settings_describe_themselves()
    {
        Assert.Equal("bloom blur 256 px wide", new BloomBlurSettings(256, null).ToString());
        Assert.Equal("bloom blur 256 px wide while a song plays", new BloomBlurSettings(null, 256).ToString());
        Assert.Equal("bloom blur 384 px wide, 256 px while a song plays", new BloomBlurSettings(384, 256).ToString());
        Assert.Equal("bloom blur 464 px while a song plays, 256 px for 1.5 s after a note hit", new BloomBlurSettings(null, 464, 256).ToString());
        Assert.Equal("bloom blur 464 px while a song plays, 256 px for 0.8 s after a note hit", new BloomBlurSettings(null, 464, 256, 0.8f).ToString());
    }

    private static List<string> WidthMessages() =>
        UnityEngine.Debug.Messages.Select(m => m.ToString()!)
            .Where(m => m.Contains(BloomBlurPatch.WidthLogText))
            .Select(m => m[m.IndexOf(BloomBlurPatch.WidthLogText, StringComparison.Ordinal)..])
            .ToList();

    private static Assembly Patch(BloomBlurSettings settings) =>
        StandIns.PatchAndLoad(StandIns.RenderingPath, m => BloomBlurPatch.Apply(m, settings));

    /// <summary>A stand-in bloom effect from a loaded copy of the stand-in Rendering assembly.</summary>
    private sealed class Effect(Assembly rendering)
    {
        private readonly object _effect = Activator.CreateInstance(rendering.GetType("PyramidBloomMainEffectSO", throwOnError: true)!)!;
        private readonly Type _renderer = rendering.GetType("PyramidBloomRendererSO", throwOnError: true)!;

        /// <summary>
        /// Renders one frame with the song flag set as given and the last note hit <paramref name="secondsSinceHit"/>
        /// seconds ago; returns the blur width and the prefilter pass used.
        /// </summary>
        public (int Width, string Prefilter) Frame(bool songPlaying, float secondsSinceHit = 1000f)
        {
            Shader.SetGlobalFloat(SongPlayingFlagPatch.GlobalName, songPlaying ? 1f : 0f);
            Time.realtimeSinceStartup = 5000f;
            Shader.SetGlobalFloat(NoteHitTimePatch.GlobalName, 5000f - secondsSinceHit);
            Blitter.Blits.Clear();
            var width = (int)_effect.GetType().GetProperty("bloomTextureWidth")!.GetValue(_effect)!;
            _effect.GetType().GetMethod("Render")!.Invoke(_effect, [new CommandBuffer(), new TextureHandle(1), new TextureHandle(2), new TextureHandle(3), Array.Empty<TextureHandle>(), 1f]);
            return (width, _renderer.GetProperty("LastPrefilterPass")!.GetValue(null)!.ToString()!);
        }
    }
}
