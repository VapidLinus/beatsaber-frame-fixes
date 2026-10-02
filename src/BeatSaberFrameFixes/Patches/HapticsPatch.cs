using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Scales rumble in <c>BeatSaber.Haptics.RumbleHapticFeedbackPlayer.PlayHapticFeedback</c>, the single method all
/// gameplay and menu rumble passes through. Presets whose asset name starts with "Hit" (note cut, bad cut, bomb,
/// chains) get the hit scales; all others get the other scales.
/// </summary>
internal static class HapticsPatch
{
    public const string FileName = "BeatSaber.Haptics.dll";

    private const string HitPresetPrefix = "Hit";

    public static void Apply(ModuleDefinition module, HapticsSettings settings)
    {
        var imports = new ModuleImports(module);
        var method = FindPlayMethod(module);
        var preset = method.Parameters.SingleOrDefault(p => p.ParameterType.Name == "HapticPresetSO")
            ?? throw new PatchTargetMismatchException("PlayHapticFeedback has no HapticPresetSO parameter");
        var strengthLoad = FindPresetLoad(method, "_strength", next => next.OpCode == OpCodes.Stfld && ((FieldReference)next.Operand).Name == "strength");
        var durationLoad = FindPresetLoad(method, "_duration", next => next.OpCode == OpCodes.Add);

        var startsWith = imports.Corlib("System.String", "StartsWith", "System.String", "System.StringComparison");
        var getName = imports.Unity("UnityEngine.Object", "get_name");
        var il = method.Body.GetILProcessor();

        // Inserted code can push short-form branches out of their 127-byte range; expand them first and re-shorten after.
        method.Body.SimplifyMacros();
        MultiplyByPresetScale(strengthLoad, settings.HitStrengthPercent, settings.OtherStrengthPercent);
        MultiplyByPresetScale(durationLoad, settings.HitDurationPercent, settings.OtherDurationPercent);
        method.Body.OptimizeMacros();

        void MultiplyByPresetScale(Instruction presetLoad, int hitPercent, int otherPercent)
        {
            var multiply = il.Create(OpCodes.Mul);
            var hitScale = il.Create(OpCodes.Ldc_R4, hitPercent / 100f);
            il.InsertAfter(presetLoad, multiply);
            foreach (var instruction in new[]
            {
                il.Create(OpCodes.Ldarg, preset),
                il.Create(OpCodes.Callvirt, getName),
                il.Create(OpCodes.Ldstr, HitPresetPrefix),
                il.Create(OpCodes.Ldc_I4, (int)StringComparison.Ordinal),
                il.Create(OpCodes.Callvirt, startsWith),
                il.Create(OpCodes.Brtrue, hitScale),
                il.Create(OpCodes.Ldc_R4, otherPercent / 100f),
                il.Create(OpCodes.Br, multiply),
                hitScale,
            })
            {
                il.InsertBefore(multiply, instruction);
            }
        }
    }

    private static MethodDefinition FindPlayMethod(ModuleDefinition module)
    {
        var player = module.GetType("BeatSaber.Haptics.RumbleHapticFeedbackPlayer")
            ?? throw new PatchTargetMismatchException("RumbleHapticFeedbackPlayer not found");
        return player.Methods.SingleOrDefault(m => m.Name == "PlayHapticFeedback" && m.HasBody)
            ?? throw new PatchTargetMismatchException("RumbleHapticFeedbackPlayer.PlayHapticFeedback not found");
    }

    /// <summary>Finds the single load of a preset field, requiring the instruction after it to match the unpatched code.</summary>
    private static Instruction FindPresetLoad(MethodDefinition method, string fieldName, Func<Instruction, bool> isExpectedNext)
    {
        var loads = method.Body.Instructions
            .Where(i => i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference f && f.Name == fieldName && f.DeclaringType.Name == "HapticPresetSO")
            .ToList();
        if (loads.Count != 1 || loads[0].Next is not { } next || !isExpectedNext(next))
            throw new PatchTargetMismatchException($"PlayHapticFeedback does not read HapticPresetSO.{fieldName} the expected way");
        return loads[0];
    }
}
