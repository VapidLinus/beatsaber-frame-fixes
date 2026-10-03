using System.Reflection;
using BeatSaberFrameFixes.Patches;
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

        Assert.Equal(512, effect.Width);
        Assert.Equal("Prefilter4", effect.RenderedPrefilter());
    }

    [Fact]
    public void Narrower_width_also_switches_to_the_13_tap_prefilter()
    {
        var effect = new Effect(Patch(256));

        Assert.Equal(256, effect.Width);
        Assert.Equal("Prefilter13", effect.RenderedPrefilter());
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    public void Game_width_or_wider_keeps_the_4_tap_prefilter(int width)
    {
        var effect = new Effect(Patch(width));

        Assert.Equal(width, effect.Width);
        Assert.Equal("Prefilter4", effect.RenderedPrefilter());
    }

    [Fact]
    public void Blur_width_and_copy_fix_can_be_combined()
    {
        var effect = new Effect(StandIns.PatchAndLoad(StandIns.RenderingPath, m =>
        {
            BloomCopyPatch.Apply(m);
            BloomBlurPatch.Apply(m, 256);
        }));

        Assert.Equal(256, effect.Width);
        Assert.Equal("Prefilter13", effect.RenderedPrefilter());
    }

    [Fact]
    public void Patching_twice_is_reported_as_a_mismatch()
    {
        var module = StandIns.ReadModule(StandIns.RenderingPath);
        BloomBlurPatch.Apply(module, 256);

        Assert.Throws<PatchTargetMismatchException>(() => BloomBlurPatch.Apply(module, 128));
    }

    [Fact]
    public void Description_mentions_the_prefilter_only_below_the_game_width()
    {
        Assert.Equal("bloom blur 256 px wide with 13-tap prefilter", BloomBlurPatch.Describe(256));
        Assert.Equal("bloom blur 512 px wide", BloomBlurPatch.Describe(512));
    }

    private static Assembly Patch(int width) =>
        StandIns.PatchAndLoad(StandIns.RenderingPath, m => BloomBlurPatch.Apply(m, width));

    /// <summary>A stand-in bloom effect from a loaded copy of the stand-in Rendering assembly.</summary>
    private sealed class Effect(Assembly rendering)
    {
        private readonly object _effect = Activator.CreateInstance(rendering.GetType("PyramidBloomMainEffectSO", throwOnError: true)!)!;
        private readonly Type _renderer = rendering.GetType("PyramidBloomRendererSO", throwOnError: true)!;

        public int Width => (int)_effect.GetType().GetProperty("bloomTextureWidth")!.GetValue(_effect)!;

        /// <summary>Renders once and returns the prefilter pass the blur was asked to use.</summary>
        public string RenderedPrefilter()
        {
            Blitter.Blits.Clear();
            _effect.GetType().GetMethod("Render")!.Invoke(_effect, [new CommandBuffer(), new TextureHandle(1), new TextureHandle(2), new TextureHandle(3), Array.Empty<TextureHandle>(), 1f]);
            return _renderer.GetProperty("LastPrefilterPass")!.GetValue(null)!.ToString()!;
        }
    }
}
