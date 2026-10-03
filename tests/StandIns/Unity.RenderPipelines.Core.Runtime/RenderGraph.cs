// Minimal stand-ins for the render graph members that the game's bloom pass uses. Names and signatures mirror
// Unity's; textures are plain ids, passes run when the test calls RenderGraph.Execute, and blits are recorded.

using System.Runtime.CompilerServices;

namespace UnityEngine.Rendering
{
    /// <summary>Stand-in for <c>UnityEngine.Rendering.MSAASamples</c>.</summary>
    public enum MSAASamples
    {
        None = 1,
        MSAA2x = 2,
        MSAA4x = 4,
        MSAA8x = 8,
    }

    /// <summary>Stand-in for <c>UnityEngine.Rendering.ContextItem</c>.</summary>
    public abstract class ContextItem
    {
    }

    /// <summary>Stand-in for <c>UnityEngine.Rendering.ContextContainer</c>, holding one item per type.</summary>
    public class ContextContainer
    {
        private readonly Dictionary<Type, ContextItem> _items = [];

        public T Get<T>() where T : ContextItem, new() => (T)_items[typeof(T)];

        public void Set<T>(T item) where T : ContextItem, new() => _items[typeof(T)] = item;
    }

    /// <summary>Stand-in for <c>UnityEngine.Rendering.UnsafeCommandBuffer</c>.</summary>
    public class UnsafeCommandBuffer
    {
        internal CommandBuffer Native { get; } = new();
    }

    /// <summary>Stand-in for <c>UnityEngine.Rendering.CommandBufferHelpers</c>.</summary>
    public static class CommandBufferHelpers
    {
        public static CommandBuffer GetNativeCommandBuffer(UnsafeCommandBuffer cmd) => cmd.Native;
    }

    /// <summary>Stand-in for <c>UnityEngine.Rendering.RTHandle</c>, wrapping the texture it was converted from.</summary>
    public class RTHandle
    {
        internal RenderGraphModule.TextureHandle Texture { get; init; }
    }

    /// <summary>One recorded blit: plain copies have no material, effect blits (such as the bloom composite) have one.</summary>
    public readonly record struct Blit(RenderGraphModule.TextureHandle Source, RenderGraphModule.TextureHandle Destination, bool WithMaterial);

    /// <summary>Stand-in for <c>UnityEngine.Rendering.Blitter</c> that records every blit in <see cref="Blits"/>.</summary>
    public static class Blitter
    {
        public static List<Blit> Blits { get; } = [];

        public static void BlitCameraTexture(CommandBuffer cmd, RTHandle source, RTHandle destination, float mipLevel = 0f, bool bilinear = false) =>
            Blits.Add(new Blit(source.Texture, destination.Texture, WithMaterial: false));

        public static void BlitCameraTexture(CommandBuffer cmd, RTHandle source, RTHandle destination, Material material, int pass) =>
            Blits.Add(new Blit(source.Texture, destination.Texture, WithMaterial: true));
    }
}

namespace UnityEngine.Rendering.RenderGraphModule
{
    /// <summary>Stand-in for <c>TextureHandle</c>: an id, where 0 is the null handle.</summary>
    public readonly record struct TextureHandle(int Id)
    {
        public static TextureHandle nullHandle => default;

        public bool IsValid() => Id != 0;

        public TextureDesc GetDescriptor(RenderGraph renderGraph) => renderGraph.GetTextureDesc(this);

        public static implicit operator RTHandle(TextureHandle texture) => new() { Texture = texture };
    }

    /// <summary>Stand-in for <c>TextureDesc</c>.</summary>
    public struct TextureDesc
    {
        public string name;

        public int width;

        public bool clearBuffer;

        public MSAASamples msaaSamples;
    }

    /// <summary>Stand-in for <c>AccessFlags</c>.</summary>
    [Flags]
    public enum AccessFlags
    {
        None = 0,
        Read = 1,
        Write = 2,
        ReadWrite = Read | Write,
    }

    /// <summary>Stand-in for <c>BaseRenderFunc</c>.</summary>
    public delegate void BaseRenderFunc<PassData, ContextType>(PassData data, ContextType renderGraphContext) where PassData : class, new();

    /// <summary>Stand-in for <c>UnsafeGraphContext</c>.</summary>
    public class UnsafeGraphContext
    {
        public UnsafeCommandBuffer cmd = new();
    }

    /// <summary>Stand-in for <c>IBaseRenderGraphBuilder</c>.</summary>
    public interface IBaseRenderGraphBuilder : IDisposable
    {
        void UseTexture(in TextureHandle input, AccessFlags flags = AccessFlags.Read);
    }

    /// <summary>Stand-in for <c>IUnsafeRenderGraphBuilder</c>.</summary>
    public interface IUnsafeRenderGraphBuilder : IBaseRenderGraphBuilder
    {
        void SetRenderFunc<PassData>(BaseRenderFunc<PassData, UnsafeGraphContext> renderFunc) where PassData : class, new();
    }

    /// <summary>Stand-in for <c>RenderGraph</c>. Recorded passes run, in order, when <see cref="Execute"/> is called.</summary>
    public class RenderGraph
    {
        private readonly List<TextureDesc> _textures = [];
        private readonly List<Action> _passes = [];

        public TextureHandle CreateTexture(in TextureDesc desc)
        {
            _textures.Add(desc);
            return new TextureHandle(_textures.Count);
        }

        public TextureDesc GetTextureDesc(TextureHandle texture) => _textures[texture.Id - 1];

        public IUnsafeRenderGraphBuilder AddUnsafePass<PassData>(string passName, out PassData passData,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) where PassData : class, new()
        {
            passData = new PassData();
            return new UnsafeBuilder<PassData>(this, passData);
        }

        public void Execute()
        {
            foreach (var pass in _passes)
                pass();
            _passes.Clear();
        }

        private sealed class UnsafeBuilder<TData>(RenderGraph graph, TData data) : IUnsafeRenderGraphBuilder where TData : class, new()
        {
            public void UseTexture(in TextureHandle input, AccessFlags flags = AccessFlags.Read)
            {
            }

            public void SetRenderFunc<PassData>(BaseRenderFunc<PassData, UnsafeGraphContext> renderFunc) where PassData : class, new() =>
                graph._passes.Add(() => renderFunc((PassData)(object)data, new UnsafeGraphContext()));

            public void Dispose()
            {
            }
        }
    }
}
