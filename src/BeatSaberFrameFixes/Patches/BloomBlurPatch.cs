using System.Globalization;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Bloom blur widths in pixels: <see cref="Width"/> applies outside songs (null keeps the game's own width, 928 in 1.45),
/// <see cref="SongWidth"/> while a song is playing (null uses <see cref="Width"/>) and <see cref="BusyWidth"/> during
/// songs for <see cref="BusySeconds"/> after a note is cut (null uses the song width).
/// </summary>
internal sealed record BloomBlurSettings(int? Width, int? SongWidth, int? BusyWidth = null, float BusySeconds = BloomBlurPatch.DefaultBusySeconds)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Width is { } width)
            parts.Add($"{width} px wide");
        if (SongWidth is { } song)
            parts.Add(Width is null && BusyWidth is null ? $"{song} px wide while a song plays" : $"{song} px while a song plays");
        if (BusyWidth is { } busy)
            parts.Add($"{busy} px for {BusySeconds.ToString("0.##", CultureInfo.InvariantCulture)} s after a note hit");
        return parts.Count == 0 ? "game bloom blur" : "bloom blur " + string.Join(", ", parts);
    }
}

/// <summary>
/// Sets the width of the bloom blur texture in <c>PyramidBloomMainEffectSO</c> by rewriting <c>bloomTextureWidth</c>,
/// which the bloom pass reads every frame. The blur pyramid starts at this width and has one level fewer per halving,
/// so a narrower texture is cheaper and drops the finest levels, which makes the glow coarser while its reach stays the
/// same. With a song width, the getter returns it while the <see cref="SongPlayingFlagPatch.GlobalName"/> shader global
/// is set; with a busy width, it returns that instead during songs while the <see cref="NoteHitTimePatch.GlobalName"/>
/// global is less than <see cref="BloomBlurSettings.BusySeconds"/> old. Whenever the current width is below
/// <see cref="FinePrefilterBelowWidth"/>, the first pass, which samples the
/// full-resolution image, uses the shader's 13-tap filter instead of the serialized 4-tap one, so thin bright lines are
/// not missed.
/// </summary>
internal static class BloomBlurPatch
{
    public const string FileName = "Rendering.dll";

    /// <summary>The bloom texture width Beat Saber 1.45 serializes in <c>PyramidBloomMainEffectSO</c>.</summary>
    public const int GameWidth = 928;

    /// <summary>
    /// Widths below this use the 13-tap prefilter. At resolution scales 1.2 to 1.4 the eye image is about 1700 to 2000 px
    /// wide, so below this width the 4-tap prefilter samples it more than about 3.4 px apart and starts skipping pixels.
    /// </summary>
    public const int FinePrefilterBelowWidth = 512;

    public const int MinWidth = 16;
    public const int MaxWidth = 2048;

    /// <summary>Default seconds after a note cut during which the busy width applies.</summary>
    public const float DefaultBusySeconds = 1.5f;

    /// <summary>Start of the Player.log line written when the width the bloom uses changes.</summary>
    public const string WidthLogText = "Bloom width now ";

    /// <summary>Width changes logged in full; after these only every 1000th change is logged.</summary>
    public const int LoggedWidthChanges = 200;

    private const string PrefilterMethodName = "FrameFixesPrefilterPass";
    private const string NoteWidthMethodName = "FrameFixesNoteWidth";

    public static void Apply(ModuleDefinition module, BloomBlurSettings settings)
    {
        var effect = module.GetType("PyramidBloomMainEffectSO") ?? throw new PatchTargetMismatchException("PyramidBloomMainEffectSO not found");
        var imports = new ModuleImports(module);
        var getter = RewriteWidth(effect, settings, imports);
        LogWidthChanges(effect, getter, imports);
        SelectPrefilterByWidth(effect, getter);
    }

    /// <summary>
    /// Passes every value the width getter returns through an injected method that logs it to Player.log when it differs
    /// from the previous one, so the width actually used during songs can be checked.
    /// </summary>
    private static void LogWidthChanges(TypeDefinition effect, MethodDefinition getter, ModuleImports imports)
    {
        var module = effect.Module;
        var int32 = module.TypeSystem.Int32;
        var lastWidth = new FieldDefinition("frameFixesLastWidth", FieldAttributes.Private | FieldAttributes.Static, int32);
        var changes = new FieldDefinition("frameFixesWidthChanges", FieldAttributes.Private | FieldAttributes.Static, int32);
        effect.Fields.Add(lastWidth);
        effect.Fields.Add(changes);

        var note = new MethodDefinition(NoteWidthMethodName, MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig, int32);
        note.Parameters.Add(new ParameterDefinition("width", ParameterAttributes.None, int32));
        var il = note.Body.GetILProcessor();
        var done = il.Create(OpCodes.Ldarg_0);
        var log = il.Create(OpCodes.Ldstr, WidthLogText + "{0} px (change {1})");
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldsfld, lastWidth);
        il.Emit(OpCodes.Beq, done);
        il.Emit(OpCodes.Ldsfld, changes);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Stsfld, changes);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Stsfld, lastWidth);
        il.Emit(OpCodes.Ldsfld, changes);
        il.Emit(OpCodes.Ldc_I4, LoggedWidthChanges);
        il.Emit(OpCodes.Ble, log);
        il.Emit(OpCodes.Ldsfld, changes);
        il.Emit(OpCodes.Ldc_I4, 1000);
        il.Emit(OpCodes.Rem);
        il.Emit(OpCodes.Brtrue, done);
        il.Append(log);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Box, int32);
        il.Emit(OpCodes.Ldsfld, changes);
        il.Emit(OpCodes.Box, int32);
        il.Emit(OpCodes.Call, imports.Corlib("System.String", "Format", "System.String", "System.Object", "System.Object"));
        il.Emit(OpCodes.Call, InjectedLog.Create(imports));
        il.Append(done);
        il.Emit(OpCodes.Ret);
        effect.Methods.Add(note);

        var getterIl = getter.Body.GetILProcessor();
        foreach (var ret in getter.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToList())
            getterIl.InsertBefore(ret, getterIl.Create(OpCodes.Call, note));
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
        var getGlobal = imports.Unity("UnityEngine.Shader", "GetGlobalFloat", "System.String");
        var outsideSong = settings.Width is { } width ? il.Create(OpCodes.Ldc_I4, width) : il.Create(OpCodes.Ldarg_0);
        var inSong = settings.SongWidth is { } songWidth ? il.Create(OpCodes.Ldc_I4, songWidth) : null;
        if (inSong is not null || settings.BusyWidth is not null)
        {
            il.Emit(OpCodes.Ldstr, SongPlayingFlagPatch.GlobalName);
            il.Emit(OpCodes.Call, getGlobal);
            il.Emit(OpCodes.Ldc_R4, 0.5f);
            il.Emit(OpCodes.Ble_Un, outsideSong);
        }
        if (settings.BusyWidth is { } busyWidth)
        {
            il.Emit(OpCodes.Call, imports.Unity("UnityEngine.Time", "get_realtimeSinceStartup"));
            il.Emit(OpCodes.Ldstr, NoteHitTimePatch.GlobalName);
            il.Emit(OpCodes.Call, getGlobal);
            il.Emit(OpCodes.Sub);
            il.Emit(OpCodes.Ldc_R4, settings.BusySeconds);
            il.Emit(OpCodes.Bge_Un, inSong ?? outsideSong);
            il.Emit(OpCodes.Ldc_I4, busyWidth);
            il.Emit(OpCodes.Ret);
        }
        if (inSong is not null)
        {
            il.Append(inSong);
            il.Emit(OpCodes.Ret);
        }
        il.Append(outsideSong);
        if (settings.Width is null)
            il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Ret);
        return getter;
    }

    /// <summary>
    /// Replaces the read of <c>_preFilterPass</c> in <c>Render</c> with an injected method that picks the 13-tap prefilter
    /// below <see cref="FinePrefilterBelowWidth"/>.
    /// </summary>
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
        il.Emit(OpCodes.Ldc_I4, FinePrefilterBelowWidth);
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
