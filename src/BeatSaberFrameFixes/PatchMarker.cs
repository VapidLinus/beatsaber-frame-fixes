using Mono.Cecil;

namespace BeatSaberFrameFixes;

/// <summary>
/// Marks a game assembly as patched by adding a <c>BeatSaberFrameFixes.PatchMarker</c> type with two string
/// constants: a description of the patch, and the SHA-256 of the original file it was made from.
/// </summary>
internal static class PatchMarker
{
    private const string Namespace = "BeatSaberFrameFixes";
    private const string TypeName = "PatchMarker";
    private const string DescriptionField = "Description";
    private const string OriginalHashField = "OriginalSha256";

    /// <param name="Description">Patcher version and settings.</param>
    /// <param name="OriginalSha256">Lowercase hex SHA-256 of the unpatched file.</param>
    public sealed record Info(string Description, string OriginalSha256);

    public static void Add(ModuleDefinition module, Info info)
    {
        var type = new TypeDefinition(Namespace, TypeName,
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            module.TypeSystem.Object);
        type.Fields.Add(Constant(module, DescriptionField, info.Description));
        type.Fields.Add(Constant(module, OriginalHashField, info.OriginalSha256));
        module.Types.Add(type);
    }

    /// <summary>Returns the recorded marker, or null if the module is not patched.</summary>
    public static Info? Read(ModuleDefinition module)
    {
        var type = module.GetType(Namespace, TypeName);
        if (type is null)
            return null;
        string Value(string name) => type.Fields.SingleOrDefault(f => f.Name == name)?.Constant as string ?? "";
        return new Info(Value(DescriptionField), Value(OriginalHashField));
    }

    private static FieldDefinition Constant(ModuleDefinition module, string name, string value) =>
        new(name, FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal, module.TypeSystem.String) { Constant = value };
}
