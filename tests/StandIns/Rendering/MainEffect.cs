using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>Stand-in for the game's main effect. <see cref="Render"/> blends the bloom from the source into the destination, as one blit with a material.</summary>
public class MainEffectSO : ScriptableObject
{
    public virtual void Render(CommandBuffer cmd, TextureHandle src, TextureHandle bloomTexture, TextureHandle dest, TextureHandle[] tempTextures, float fade) =>
        Blitter.BlitCameraTexture(cmd, src, dest, new Material(), 0);
}

/// <summary>Stand-in for the game's bloom pyramid renderer; records the prefilter pass of the last blur.</summary>
public class PyramidBloomRendererSO : ScriptableObject
{
    public enum Pass
    {
        Prefilter13,
        Prefilter4,
        Downsample13,
        Downsample4,
    }

    public static Pass? LastPrefilterPass { get; set; }

    public void RenderBloom(CommandBuffer cmd, TextureHandle src, TextureHandle dest, Pass preFilterPass, Pass downsamplePass)
    {
        LastPrefilterPass = preFilterPass;
    }
}

/// <summary>Stand-in for the game's bloom main effect, with the serialized blur texture width and passes.</summary>
public class PyramidBloomMainEffectSO : MainEffectSO
{
    private readonly PyramidBloomRendererSO _bloomRenderer = new();

    private int _bloomTextureWidth = 928;

    private PyramidBloomRendererSO.Pass _preFilterPass = PyramidBloomRendererSO.Pass.Prefilter4;

    private PyramidBloomRendererSO.Pass _downsamplePass = PyramidBloomRendererSO.Pass.Downsample4;

    public int bloomTextureWidth => _bloomTextureWidth;

    public override void Render(CommandBuffer cmd, TextureHandle src, TextureHandle bloomTexture, TextureHandle dest, TextureHandle[] tempTextures, float fade)
    {
        _bloomRenderer.RenderBloom(cmd, src, bloomTexture, _preFilterPass, _downsamplePass);
        base.Render(cmd, src, bloomTexture, dest, tempTextures, fade);
    }
}

/// <summary>Stand-in for the game's main effect container.</summary>
public class MainEffectContainerSO : ScriptableObject
{
    public MainEffectSO mainEffect { get; } = new();
}

/// <summary>
/// Stand-in for the game's bloom render pass. <see cref="RecordRenderGraph"/> and <see cref="ExecutePyramidBloomPass"/>
/// follow the game's methods: the camera color is copied to a temporary texture when MSAA is off, and the bloom is
/// blended from that copy back into the camera color.
/// </summary>
internal class MainEffectPostRenderPass : ScriptableRenderPass
{
    private class PassData
    {
        public MainEffectContainerSO effectContainer = null!;

        public float fadeValue;

        public TextureHandle activeColorTexture;

        public TextureHandle copyColor;

        public TextureHandle bloomTexture;

        public TextureHandle[] tempTextures = [];
    }

    private readonly MainEffectContainerSO _container = new();

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        if (cameraData == null)
        {
            return;
        }
        requiresIntermediateTexture = true;
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        if (!resourceData.cameraColor.IsValid())
        {
            return;
        }
        using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass<PassData>("MainEffect_PostRenderPass_PyramidBloom", out PassData passData);
        TextureHandle bloomTexture = renderGraph.CreateTexture(new TextureDesc { name = "_MainEffectBloomTexture", width = cameraData.pixelWidth / 4 });
        TextureDesc desc = resourceData.activeColorTexture.GetDescriptor(renderGraph);
        if (desc.msaaSamples == MSAASamples.None)
        {
            desc.name = "_MainEffectCopyColorTexture";
            desc.clearBuffer = false;
            passData.copyColor = renderGraph.CreateTexture(in desc);
        }
        else
        {
            passData.copyColor = TextureHandle.nullHandle;
        }
        passData.fadeValue = 1f;
        passData.effectContainer = _container;
        passData.activeColorTexture = resourceData.activeColorTexture;
        passData.bloomTexture = bloomTexture;
        passData.tempTextures = [];
        if (passData.copyColor.IsValid())
        {
            builder.UseTexture(in passData.copyColor, AccessFlags.ReadWrite);
        }
        builder.UseTexture(in passData.bloomTexture, AccessFlags.ReadWrite);
        builder.UseTexture(in passData.activeColorTexture, AccessFlags.ReadWrite);
        builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePyramidBloomPass(data, context));
    }

    private static void ExecutePyramidBloomPass(PassData passData, UnsafeGraphContext context)
    {
        CommandBuffer nativeCommandBuffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
        MainEffectSO mainEffect = passData.effectContainer.mainEffect;
        if (passData.copyColor.IsValid())
        {
            Blitter.BlitCameraTexture(nativeCommandBuffer, passData.activeColorTexture, passData.copyColor);
            mainEffect.Render(nativeCommandBuffer, passData.copyColor, passData.bloomTexture, passData.activeColorTexture, passData.tempTextures, passData.fadeValue);
        }
        else
        {
            mainEffect.Render(nativeCommandBuffer, passData.activeColorTexture, passData.bloomTexture, passData.activeColorTexture, passData.tempTextures, passData.fadeValue);
        }
    }
}
