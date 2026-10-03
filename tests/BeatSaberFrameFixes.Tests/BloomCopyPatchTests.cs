using System.Reflection;
using BeatSaberFrameFixes.Patches;
using Mono.Cecil;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace BeatSaberFrameFixes.Tests;

[Collection(nameof(UnityStaticState))]
public class BloomCopyPatchTests
{
    private static readonly Assembly Patched = StandIns.PatchAndLoad(StandIns.RenderingPath, BloomCopyPatch.Apply);
    private static readonly Assembly Unpatched = StandIns.PatchAndLoad(StandIns.RenderingPath, _ => { });

    [Fact]
    public void Unpatched_bloom_copies_the_camera_image_and_blends_back_into_it()
    {
        var frame = new Frame(Unpatched);

        frame.Render();

        Assert.Equal([Copy(frame.CameraColor, frame.CopyColor), Composite(frame.CopyColor, frame.CameraColor)], Blitter.Blits);
        Assert.Equal(frame.CameraColor, frame.Resources.cameraColor);
    }

    [Fact]
    public void Patched_bloom_blends_into_the_copy_texture_and_makes_it_the_camera_image()
    {
        var frame = new Frame(Patched);

        frame.Render();

        Assert.Equal([Composite(frame.CameraColor, frame.CopyColor)], Blitter.Blits);
        Assert.Equal(frame.CopyColor, frame.Resources.cameraColor);
    }

    [Theory]
    [InlineData(false, CameraRenderType.Base)]
    [InlineData(true, CameraRenderType.Overlay)]
    public void Stacked_cameras_keep_the_copy(bool resolveFinalTarget, CameraRenderType renderType)
    {
        var frame = new Frame(Patched, resolveFinalTarget, renderType);

        frame.Render();

        Assert.Equal([Copy(frame.CameraColor, frame.CopyColor), Composite(frame.CopyColor, frame.CameraColor)], Blitter.Blits);
        Assert.Equal(frame.CameraColor, frame.Resources.cameraColor);
    }

    [Fact]
    public void Msaa_keeps_the_game_behavior()
    {
        var frame = new Frame(Patched, msaa: MSAASamples.MSAA4x);

        frame.Render();

        Assert.Equal([Composite(frame.CameraColor, frame.CameraColor)], Blitter.Blits);
        Assert.Equal(frame.CameraColor, frame.Resources.cameraColor);
    }

    [Fact]
    public void First_swap_is_logged_once()
    {
        var assembly = StandIns.PatchAndLoad(StandIns.RenderingPath, BloomCopyPatch.Apply);
        UnityEngine.Debug.Messages.Clear();

        new Frame(assembly).Render();
        new Frame(assembly).Render();

        Assert.Single(UnityEngine.Debug.Messages, m => m.ToString()!.Contains(BloomCopyPatch.SwapLogMessage));
    }

    [Fact]
    public void Already_patched_pass_is_reported_as_a_mismatch()
    {
        var module = StandIns.ReadModule(StandIns.RenderingPath);
        BloomCopyPatch.Apply(module);

        Assert.Throws<PatchTargetMismatchException>(() => BloomCopyPatch.Apply(module));
    }

    [Fact]
    public void Pass_without_the_copy_is_reported_as_a_mismatch()
    {
        var module = StandIns.ReadModule(StandIns.RenderingPath);
        var execute = module.GetType("MainEffectPostRenderPass").Methods.Single(m => m.Name == "ExecutePyramidBloomPass");
        var blit = execute.Body.Instructions.Single(i => i.Operand is MethodReference { Name: "BlitCameraTexture" });
        execute.Body.GetILProcessor().Replace(blit, Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Pop));

        Assert.Throws<PatchTargetMismatchException>(() => BloomCopyPatch.Apply(module));
    }

    private static Blit Copy(TextureHandle source, TextureHandle destination) => new(source, destination, WithMaterial: false);

    private static Blit Composite(TextureHandle source, TextureHandle destination) => new(source, destination, WithMaterial: true);

    /// <summary>One frame of the stand-in bloom pass, with a camera image created up front.</summary>
    private sealed class Frame
    {
        private readonly RenderGraph _graph = new();
        private readonly ContextContainer _context = new();
        private readonly ScriptableRenderPass _pass;

        public Frame(Assembly rendering, bool resolveFinalTarget = true, CameraRenderType renderType = CameraRenderType.Base, MSAASamples msaa = MSAASamples.None)
        {
            Blitter.Blits.Clear();
            CameraColor = _graph.CreateTexture(new TextureDesc { name = "_CameraTargetAttachment", msaaSamples = msaa });
            Resources = new UniversalResourceData { cameraColor = CameraColor };
            _context.Set(new UniversalCameraData { resolveFinalTarget = resolveFinalTarget, renderType = renderType });
            _context.Set(Resources);
            _pass = (ScriptableRenderPass)Activator.CreateInstance(rendering.GetType("MainEffectPostRenderPass", throwOnError: true)!, nonPublic: true)!;
        }

        public TextureHandle CameraColor { get; }

        /// <summary>The texture the pass creates for the copy; the pass creates the bloom texture first.</summary>
        public TextureHandle CopyColor => new(CameraColor.Id + 2);

        public UniversalResourceData Resources { get; }

        public void Render()
        {
            _pass.RecordRenderGraph(_graph, _context);
            _graph.Execute();
        }
    }
}
