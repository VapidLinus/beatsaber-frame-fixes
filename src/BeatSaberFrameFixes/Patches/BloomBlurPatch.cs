using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Bloom blur widths in pixels: <see cref="Width"/> applies outside songs (null keeps the game's 512) and
/// <see cref="SongWidth"/> while a song is playing (null uses <see cref="Width"/>).
/// </summary>
internal sealed record BloomBlurSettings(int? Width, int? SongWidth)
{
    public override string ToString() => (Width, SongWidth) switch
    {
        ({ } w, { } s) => $"bloom blur {w} px wide, {s} px while a song plays",
        ({ } w, null) => $"bloom blur {w} px wide",
        (null, { } s) => $"bloom blur {s} px wide while a song plays",
        _ => "game bloom blur",
    };
}

/// <summary>
/// Sets the width of the bloom blur texture in <c>PyramidBloomMainEffectSO</c> by rewriting <c>bloomTextureWidth</c>,
/// which the bloom pass reads every frame. The blur pyramid starts at this width and has one level fewer per halving,
/// so a narrower texture is cheaper and drops the finest levels, which makes the glow coarser while its reach stays the
/// same. With a song width, the getter returns it while the <see cref="SongPlayingFlagPatch.GlobalName"/> shader global
/// is set. Whenever the current width is below 512, the first pass, which samples the full-resolution image, uses the
/// shader's 13-tap filter instead of the serialized 4-tap one, so thin bright lines are not missed.
/// </summary>
internal static class BloomBlurPatch
{
    public const string FileName = "Rendering.dll";

    public const int GameWidth = 512;
    public const int MinWidth = 16;
    public const int MaxWidth = 2048;

    private const string PrefilterMethodName = "FrameFixesPrefilterPass";

    public static void Apply(ModuleDefinition module, BloomBlurSettings settings)
    {
        var effect = module.GetType("PyramidBloomMainEffectSO") ?? throw new PatchTargetMismatchException("PyramidBloomMainEffectSO not found");
        var getter = RewriteWidth(effect, settings, new ModuleImports(module));
        SelectPrefilterByWidth(effect, getter);
    }

    private static MethodDefinition RewriteWidth(TypeDefinition effect, BloomBlurSettings settings, ModuleImports imports)
    {
        var getter = effect.Methods.SingleOrDefault(m => m.Name == "get_bloomTextureWidth" && m.HasBody)
            ?? throw new PatchTargetMismatchException("PyramidBloomMainEffectSO.bloomTextureWidth not found");
        if (getter.Body.Instructions.Where(i => i.OpCode != OpCodes.Nop).ToList() is not
            [{ OpCode.Code: Code.Ldarg_0 }, { OpCode.Code: Code.Ldfld, Operand: FieldReference field }, { OpCode.Code: Code.Ret }]
            || field.Name != "_bloomTextureWidth")
            throw new PatchTargetMismatchException("PyramidBloomMainEffectSO.bloomTextureWidth does not return _bloomTextureWidth");

        var il = getter.Body.GetILProcessor();
        getter.Body.Instructions.Clear();
        var outsideSong = settings.Width is { } width ? il.Create(OpCodes.Ldc_I4, width) : il.Create(OpCodes.Ldarg_0);
        if (settings.SongWidth is { } songWidth)
        {
            il.Emit(OpCodes.Ldstr, SongPlayingFlagPatch.GlobalName);
            il.Emit(OpCodes.Call, imports.Unity("UnityEngine.Shader", "GetGlobalFloat", "System.String"));
            il.Emit(OpCodes.Ldc_R4, 0.5f);
            il.Emit(OpCodes.Ble_Un, outsideSong);
            il.Emit(OpCodes.Ldc_I4, songWidth);
            il.Emit(OpCodes.Ret);
        }
        il.Append(outsideSong);
        if (settings.Width is null)
            il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Ret);
        return getter;
    }

    /// <summary>Replaces the read of <c>_preFilterPass</c> in <c>Render</c> with an injected method that picks the 13-tap prefilter below 512 px.</summary>
    private static void SelectPrefilterByWidth(TypeDefinition effect, MethodDefinition widthGetter)
    {
        var render = effect.Methods.SingleOrDefault(m => m.Name == "Render" && m.HasBody)
            ?? throw new PatchTargetMismatchException("PyramidBloomMainEffectSO.Render not found");
        var loads = render.Body.Instructions
            .Where(i => i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference { Name: "_preFilterPass" })
            .ToList();
        if (loads is not [var load] || effect.Methods.Any(m => m.Name == PrefilterMethodName))
            throw new PatchTargetMismatchException("PyramidBloomMainEffectSO.Render does not read _preFilterPass the expected way");

        var preFilterPass = (FieldReference)load.Operand;
        var prefilter13 = preFilterPass.FieldType.Resolve()?.Fields.SingleOrDefault(f => f.Name == "Prefilter13" && f.HasConstant)
            ?? throw new PatchTargetMismatchException("PyramidBloomRendererSO.Pass.Prefilter13 not found");

        var method = new MethodDefinition(PrefilterMethodName, MethodAttributes.Private | MethodAttributes.HideBySig, preFilterPass.FieldType);
        var il = method.Body.GetILProcessor();
        var serialized = il.Create(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, widthGetter);
        il.Emit(OpCodes.Ldc_I4, GameWidth);
        il.Emit(OpCodes.Bge, serialized);
        il.Emit(OpCodes.Ldc_I4, Convert.ToInt32(prefilter13.Constant));
        il.Emit(OpCodes.Ret);
        il.Append(serialized);
        il.Emit(OpCodes.Ldfld, preFilterPass);
        il.Emit(OpCodes.Ret);
        effect.Methods.Add(method);

        render.Body.GetILProcessor().Replace(load, Instruction.Create(OpCodes.Call, method));
    }
}
