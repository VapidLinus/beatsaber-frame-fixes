using Mono.Cecil;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Imports framework and UnityEngine members into a game module. Members are resolved from the assemblies the module
/// itself references (the game's own mscorlib and UnityEngine.CoreModule), never from the patcher's runtime.
/// </summary>
internal sealed class ModuleImports(ModuleDefinition module)
{
    private TypeDefinition? _corlibString;
    private ModuleDefinition? _unityCore;

    public ModuleDefinition Module => module;

    public MethodReference Corlib(string typeName, string methodName, params string[] parameterTypes)
    {
        var type = CorlibString.Module.GetType(typeName) ?? throw new PatchTargetMismatchException($"Type {typeName} not found in the game's runtime library");
        return Import(type, methodName, parameterTypes);
    }

    public TypeReference CorlibType(string typeName) =>
        module.ImportReference(CorlibString.Module.GetType(typeName) ?? throw new PatchTargetMismatchException($"Type {typeName} not found in the game's runtime library"));

    public MethodReference Unity(string typeName, string methodName, params string[] parameterTypes)
    {
        var type = UnityCore.GetType(typeName) ?? throw new PatchTargetMismatchException($"Type {typeName} not found in UnityEngine.CoreModule");
        return Import(type, methodName, parameterTypes);
    }

    private TypeDefinition CorlibString => _corlibString ??= module.TypeSystem.String.Resolve()
        ?? throw new PatchTargetMismatchException("The game's runtime library could not be resolved");

    private ModuleDefinition UnityCore => _unityCore ??= ResolveUnityCore();

    private ModuleDefinition ResolveUnityCore()
    {
        var reference = module.AssemblyReferences.FirstOrDefault(a => a.Name == "UnityEngine.CoreModule")
            ?? throw new PatchTargetMismatchException($"{module.Name} does not reference UnityEngine.CoreModule");
        return module.AssemblyResolver.Resolve(reference).MainModule;
    }

    private MethodReference Import(TypeDefinition type, string methodName, string[] parameterTypes)
    {
        var method = type.Methods.SingleOrDefault(m => m.Name == methodName
            && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameterTypes))
            ?? throw new PatchTargetMismatchException($"{type.FullName}.{methodName}({string.Join(", ", parameterTypes)}) not found");
        return module.ImportReference(method);
    }
}
