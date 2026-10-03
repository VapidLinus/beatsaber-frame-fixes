using System.Reflection;
using System.Runtime.Loader;
using Mono.Cecil;

namespace BeatSaberFrameFixes.Tests;

/// <summary>
/// Access to the stand-in game assemblies built next to the tests, and helpers to patch a copy of one and load the
/// result. Loaded copies share the default context's <c>UnityEngine.CoreModule</c>, so tests can drive
/// <c>UnityEngine.Time</c> and read <c>UnityEngine.Debug.Messages</c> directly.
/// </summary>
internal static class StandIns
{
    public static string HapticsPath => Path.Combine(AppContext.BaseDirectory, "BeatSaber.Haptics.dll");

    public static string MainPath => Path.Combine(AppContext.BaseDirectory, "Main.dll");

    public static string UnityCorePath => Path.Combine(AppContext.BaseDirectory, "UnityEngine.CoreModule.dll");

    public static string RenderingPath => Path.Combine(AppContext.BaseDirectory, "Rendering.dll");

    /// <summary>Every stand-in game assembly, as a fake game's Managed folder needs them.</summary>
    public static IEnumerable<string> AllPaths =>
    [
        MainPath, HapticsPath, UnityCorePath, RenderingPath,
        Path.Combine(AppContext.BaseDirectory, "Unity.RenderPipelines.Core.Runtime.dll"),
        Path.Combine(AppContext.BaseDirectory, "Unity.RenderPipelines.Universal.Runtime.dll"),
    ];

    public static ModuleDefinition ReadModule(string path) =>
        ModuleDefinition.ReadModule(path, new ReaderParameters { AssemblyResolver = CreateResolver(), InMemory = true });

    /// <summary>Applies <paramref name="patch"/> to a copy of the assembly at <paramref name="path"/> and loads it.</summary>
    public static Assembly PatchAndLoad(string path, Action<ModuleDefinition> patch)
    {
        using var module = ReadModule(path);
        patch(module);
        var stream = new MemoryStream();
        module.Write(stream);
        stream.Position = 0;
        return new IsolatedContext().LoadFromStream(stream);
    }

    private static DefaultAssemblyResolver CreateResolver()
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(AppContext.BaseDirectory);
        return resolver;
    }

    private sealed class IsolatedContext() : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
