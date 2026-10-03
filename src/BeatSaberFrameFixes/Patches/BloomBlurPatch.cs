using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Sets the width of the bloom blur texture in <c>PyramidBloomMainEffectSO</c> by making <c>bloomTextureWidth</c> return
/// a fixed value instead of the serialized 512. The blur pyramid starts at this width and has one level fewer per
/// halving, so a narrower texture is cheaper and drops the finest levels, which makes the glow coarser while its reach
/// stays the same. Below 512, the first pass, which samples the full-resolution image, also switches from the
/// serialized 4-tap filter to the shader's 13-tap filter, so thin bright lines are not missed at the larger downscale.
/// </summary>
internal static class BloomBlurPatch
{
    public const string FileName = "Rendering.dll";

    public const int GameWidth = 512;
    public const int MinWidth = 16;
    public const int MaxWidth = 2048;

    public static void Apply(ModuleDefinition module, int width)
    {
        var effect = module.GetType("PyramidBloomMainEffectSO") ?? throw new PatchTargetMismatchException("PyramidBloomMainEffectSO not found");
        SetWidth(effect, width);
        if (width < GameWidth)
            UsePrefilter13(effect);
    }

    public static string Describe(int width) =>
        width < GameWidth ? $"bloom blur {width} px wide with 13-tap prefilter" : $"bloom blur {width} px wide";

    private static void SetWidth(TypeDefinition effect, int width)
    {
        var getter = effect.Methods.SingleOrDefault(m => m.Name == "get_bloomTextureWidth" && m.HasBody)
            ?? throw new PatchTargetMismatchException("PyramidBloomMainEffectSO.bloomTextureWidth not found");
        if (getter.Body.Instructions.Where(i => i.OpCode != OpCodes.Nop).ToList() is not
            [{ OpCode.Code: Code.Ldarg_0 }, { OpCode.Code: Code.Ldfld, Operand: FieldReference { Name: "_bloomTextureWidth" } }, { OpCode.Code: Code.Ret }])
            throw new PatchTargetMismatchException("PyramidBloomMainEffectSO.bloomTextureWidth does not return _bloomTextureWidth");

        var il = getter.Body.GetILProcessor();
        getter.Body.Instructions.Clear();
        il.Emit(OpCodes.Ldc_I4, width);
        il.Emit(OpCodes.Ret);
    }

    private static void UsePrefilter13(TypeDefinition effect)
    {
        var render = effect.Methods.SingleOrDefault(m => m.Name == "Render" && m.HasBody)
            ?? throw new PatchTargetMismatchException("PyramidBloomMainEffectSO.Render not found");
        var loads = render.Body.Instructions
            .Where(i => i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference { Name: "_preFilterPass" })
            .ToList();
        if (loads is not [var load])
            throw new PatchTargetMismatchException("PyramidBloomMainEffectSO.Render does not read _preFilterPass the expected way");

        var passType = ((FieldReference)load.Operand).FieldType.Resolve()
            ?? throw new PatchTargetMismatchException("PyramidBloomRendererSO.Pass could not be resolved");
        var prefilter13 = passType.Fields.SingleOrDefault(f => f.Name == "Prefilter13" && f.HasConstant)
            ?? throw new PatchTargetMismatchException("PyramidBloomRendererSO.Pass.Prefilter13 not found");

        var il = render.Body.GetILProcessor();
        il.InsertAfter(load, il.Create(OpCodes.Ldc_I4, Convert.ToInt32(prefilter13.Constant)));
        il.Replace(load, il.Create(OpCodes.Pop));
    }
}
