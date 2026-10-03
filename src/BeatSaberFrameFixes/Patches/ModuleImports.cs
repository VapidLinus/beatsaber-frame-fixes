using Mono.Cecil;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Imports framework, UnityEngine and other game members into a game module. Members are resolved from the assemblies
/// the module itself references (the game's own mscorlib, UnityEngine.CoreModule and so on), never from the patcher's
/// runtime.
/// </summary>
internal sealed class ModuleImports(ModuleDefinition module)
{
    private readonly Dictionary<string, ModuleDefinition> _referenced = [];
    private TypeDefinition? _corlibString;

    public ModuleDefinition Module => module;

    public MethodReference Corlib(string typeName, string methodName, params string[] parameterTypes)
    {
        var type = CorlibString.Module.GetType(typeName) ?? throw new PatchTargetMismatchException($"Type {typeName} not found in the game's runtime library");
        return Import(type, methodName, parameterTypes);
    }

    public TypeReference CorlibType(string typeName) =>
        module.ImportReference(CorlibString.Module.GetType(typeName) ?? throw new PatchTargetMismatchException($"Type {typeName} not found in the game's runtime library"));

    public MethodReference Unity(string typeName, string methodName, params string[] parameterTypes) =>
        Method("UnityEngine.CoreModule", typeName, methodName, parameterTypes);

    /// <summary>Imports a method declared in <paramref name="assemblyName"/>, which the module must reference.</summary>
    public MethodReference Method(string assemblyName, string typeName, string methodName, params string[] parameterTypes) =>
        Import(ReferencedType(assemblyName, typeName), methodName, parameterTypes);

    /// <summary>Imports a field declared in <paramref name="assemblyName"/>, which the module must reference.</summary>
    public FieldReference Field(string assemblyName, string typeName, string fieldName)
    {
        var field = ReferencedType(assemblyName, typeName).Fields.SingleOrDefault(f => f.Name == fieldName)
            ?? throw new PatchTargetMismatchException($"{typeName}.{fieldName} not found");
        return module.ImportReference(field);
    }

    private TypeDefinition CorlibString => _corlibString ??= module.TypeSystem.String.Resolve()
        ?? throw new PatchTargetMismatchException("The game's runtime library could not be resolved");

    private TypeDefinition ReferencedType(string assemblyName, string typeName) =>
        Referenced(assemblyName).GetType(typeName) ?? throw new PatchTargetMismatchException($"Type {typeName} not found in {assemblyName}");

    private ModuleDefinition Referenced(string assemblyName)
    {
        if (_referenced.TryGetValue(assemblyName, out var resolved))
            return resolved;
        var reference = module.AssemblyReferences.FirstOrDefault(a => a.Name == assemblyName)
            ?? throw new PatchTargetMismatchException($"{module.Name} does not reference {assemblyName}");
        return _referenced[assemblyName] = module.AssemblyResolver.Resolve(reference).MainModule;
    }

    private MethodReference Import(TypeDefinition type, string methodName, string[] parameterTypes)
    {
        var method = type.Methods.SingleOrDefault(m => m.Name == methodName
            && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameterTypes))
            ?? throw new PatchTargetMismatchException($"{type.FullName}.{methodName}({string.Join(", ", parameterTypes)}) not found");
        return module.ImportReference(method);
    }
}
