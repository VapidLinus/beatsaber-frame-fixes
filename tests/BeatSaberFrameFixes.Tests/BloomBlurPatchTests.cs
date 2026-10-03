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

        Assert.Equal((512, "Prefilter4"), effect.Frame(songPlaying: false));
    }

    [Fact]
    public void Narrower_width_also_switches_to_the_13_tap_prefilter()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(256, SongWidth: null)));

        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: false));
        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: true));
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    public void Game_width_or_wider_keeps_the_4_tap_prefilter(int width)
    {
        var effect = new Effect(Patch(new BloomBlurSettings(width, SongWidth: null)));

        Assert.Equal((width, "Prefilter4"), effect.Frame(songPlaying: false));
    }

    [Fact]
    public void Song_width_applies_only_while_a_song_plays()
    {
        var effect = new Effect(Patch(new BloomBlurSettings(Width: null, SongWidth: 256)));

        Assert.Equal((512, "Prefilter4"), effect.Frame(songPlaying: false));
        Assert.Equal((256, "Prefilter13"), effect.Frame(songPlaying: true));
        Assert.Equal((512, "Prefilter4"), effect.Frame(songPlaying: false));
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
    }

    private static Assembly Patch(BloomBlurSettings settings) =>
        StandIns.PatchAndLoad(StandIns.RenderingPath, m => BloomBlurPatch.Apply(m, settings));

    /// <summary>A stand-in bloom effect from a loaded copy of the stand-in Rendering assembly.</summary>
    private sealed class Effect(Assembly rendering)
    {
        private readonly object _effect = Activator.CreateInstance(rendering.GetType("PyramidBloomMainEffectSO", throwOnError: true)!)!;
        private readonly Type _renderer = rendering.GetType("PyramidBloomRendererSO", throwOnError: true)!;

        /// <summary>Renders one frame with the song flag set as given; returns the blur width and the prefilter pass used.</summary>
        public (int Width, string Prefilter) Frame(bool songPlaying)
        {
            Shader.SetGlobalFloat(SongPlayingFlagPatch.GlobalName, songPlaying ? 1f : 0f);
            Blitter.Blits.Clear();
            var width = (int)_effect.GetType().GetProperty("bloomTextureWidth")!.GetValue(_effect)!;
            _effect.GetType().GetMethod("Render")!.Invoke(_effect, [new CommandBuffer(), new TextureHandle(1), new TextureHandle(2), new TextureHandle(3), Array.Empty<TextureHandle>(), 1f]);
            return (width, _renderer.GetProperty("LastPrefilterPass")!.GetValue(null)!.ToString()!);
        }
    }
}
