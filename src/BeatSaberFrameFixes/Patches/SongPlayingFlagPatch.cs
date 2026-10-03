using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Publishes whether a song is playing as the Unity shader global <see cref="GlobalName"/> (1 while playing, otherwise
/// 0), for patched code in other game assemblies. <c>AudioTimeSyncController.Update</c> sets it every frame from the
/// song clock's state, and <c>OnDestroy</c> clears it when the gameplay scene ends.
/// </summary>
internal static class SongPlayingFlagPatch
{
    public const string FileName = "Main.dll";

    public const string GlobalName = "_BeatSaberFrameFixesSongPlaying";

    public static void Apply(ModuleDefinition module)
    {
        var imports = new ModuleImports(module);
        var controller = module.GetType("AudioTimeSyncController") ?? throw new PatchTargetMismatchException("AudioTimeSyncController not found");
        var update = FindMethod(controller, "Update");
        var onDestroy = FindMethod(controller, "OnDestroy");
        var state = controller.Fields.SingleOrDefault(f => f.Name == "_state")
            ?? throw new PatchTargetMismatchException("AudioTimeSyncController._state not found");
        var playing = state.FieldType.Resolve()?.Fields.SingleOrDefault(f => f.Name == "Playing" && f.HasConstant)
            ?? throw new PatchTargetMismatchException("AudioTimeSyncController state has no Playing value");
        var setGlobal = imports.Unity("UnityEngine.Shader", "SetGlobalFloat", "System.String", "System.Single");

        Prepend(update,
        [
            Instruction.Create(OpCodes.Ldstr, GlobalName),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldfld, state),
            Instruction.Create(OpCodes.Ldc_I4, Convert.ToInt32(playing.Constant)),
            Instruction.Create(OpCodes.Ceq),
            Instruction.Create(OpCodes.Conv_R4),
            Instruction.Create(OpCodes.Call, setGlobal),
        ]);
        Prepend(onDestroy,
        [
            Instruction.Create(OpCodes.Ldstr, GlobalName),
            Instruction.Create(OpCodes.Ldc_R4, 0f),
            Instruction.Create(OpCodes.Call, setGlobal),
        ]);
    }

    private static void Prepend(MethodDefinition method, Instruction[] block)
    {
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        foreach (var instruction in block)
            il.InsertBefore(first, instruction);
    }

    private static MethodDefinition FindMethod(TypeDefinition type, string name) =>
        type.Methods.SingleOrDefault(m => m.Name == name && m.Parameters.Count == 0 && m.HasBody)
        ?? throw new PatchTargetMismatchException($"{type.Name}.{name} not found");
}
