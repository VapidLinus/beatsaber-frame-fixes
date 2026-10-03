// Minimal stand-ins for the URP members that the game's bloom pass and the bloom patch use.

using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>Stand-in for <c>CameraRenderType</c>.</summary>
    public enum CameraRenderType
    {
        Base,
        Overlay,
    }

    /// <summary>Stand-in for <c>UniversalCameraData</c>.</summary>
    public class UniversalCameraData : ContextItem
    {
        public CameraRenderType renderType;

        public int pixelWidth = 1920;

        /// <summary>True when the camera is the last one in its stack and renders to the final target.</summary>
        public bool resolveFinalTarget;
    }

    /// <summary>Stand-in for <c>UniversalResourceData</c>, where the active color texture is always the camera color.</summary>
    public class UniversalResourceData : ContextItem
    {
        public TextureHandle cameraColor { get; set; }

        public TextureHandle activeColorTexture => cameraColor;
    }

    /// <summary>Stand-in for <c>ScriptableRenderPass</c>.</summary>
    public abstract class ScriptableRenderPass
    {
        public bool requiresIntermediateTexture { get; set; }

        public virtual void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
        }
    }
}
