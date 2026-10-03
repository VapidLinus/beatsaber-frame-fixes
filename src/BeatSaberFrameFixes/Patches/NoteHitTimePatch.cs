using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Publishes when a note was last cut as the Unity shader global <see cref="GlobalName"/>, holding
/// <c>Time.realtimeSinceStartup</c> at the cut, for patched code in other game assemblies.
/// <c>NoteController.SendNoteWasCutEvent</c>, which runs once for every cut note, sets it.
/// </summary>
internal static class NoteHitTimePatch
{
    public const string FileName = "Main.dll";

    public const string GlobalName = "_BeatSaberFrameFixesLastNoteHit";

    public static void Apply(ModuleDefinition module)
    {
        var imports = new ModuleImports(module);
        var note = module.GetType("NoteController") ?? throw new PatchTargetMismatchException("NoteController not found");
        var sendCut = note.Methods.SingleOrDefault(m => m.Name == "SendNoteWasCutEvent" && m.Parameters.Count == 1 && m.HasBody)
            ?? throw new PatchTargetMismatchException("NoteController.SendNoteWasCutEvent not found");

        var il = sendCut.Body.GetILProcessor();
        var first = sendCut.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldstr, GlobalName));
        il.InsertBefore(first, il.Create(OpCodes.Call, imports.Unity("UnityEngine.Time", "get_realtimeSinceStartup")));
        il.InsertBefore(first, il.Create(OpCodes.Call, imports.Unity("UnityEngine.Shader", "SetGlobalFloat", "System.String", "System.Single")));
    }
}
