using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Removes a full-screen copy from the bloom post-process in <c>MainEffectPostRenderPass</c>. The game copies the
/// camera image to a temporary texture and blends the bloom from that copy back into the camera image. Patched, the
/// bloom is blended from the camera image into the temporary texture, which then becomes the camera image, as URP's
/// own post-processing does. This only applies to a base camera that renders to the final target with MSAA off;
/// stacked cameras and MSAA keep the game's behavior.
/// </summary>
internal static class BloomCopyPatch
{
    public const string FileName = "Rendering.dll";

    public const string SwapLogMessage = "Bloom is rendering without the full-screen copy";

    private const string SwapFieldName = "frameFixesSwapColorTarget";
    private const string LoggedFieldName = "frameFixesSwapLogged";
    private const string UniversalRuntime = "Unity.RenderPipelines.Universal.Runtime";

    public static void Apply(ModuleDefinition module)
    {
        var pass = module.GetType("MainEffectPostRenderPass") ?? throw new PatchTargetMismatchException("MainEffectPostRenderPass not found");
        var passData = pass.NestedTypes.SingleOrDefault(t => t.Name == "PassData")
            ?? throw new PatchTargetMismatchException("MainEffectPostRenderPass.PassData not found");
        if (passData.Fields.Any(f => f.Name == SwapFieldName))
            throw new PatchTargetMismatchException("MainEffectPostRenderPass is already patched");

        var fields = new PassDataFields(passData);
        var record = FindMethod(pass, "RecordRenderGraph");
        var execute = FindMethod(pass, "ExecutePyramidBloomPass");
        var swap = new FieldDefinition(SwapFieldName, FieldAttributes.Public, module.TypeSystem.Boolean);
        passData.Fields.Add(swap);
        var logged = new FieldDefinition(LoggedFieldName, FieldAttributes.Private | FieldAttributes.Static, module.TypeSystem.Boolean);
        pass.Fields.Add(logged);

        var imports = new ModuleImports(module);
        PatchRecordRenderGraph(record, fields, swap, logged, imports);
        PatchExecute(execute, fields, swap);
    }

    /// <summary>
    /// After the pass data captures the camera image, swaps the camera image for the copy texture when the swap is safe
    /// and flags the pass data so <see cref="PatchExecute"/> blends into the copy texture instead of copying. The first
    /// swap is logged to Player.log.
    /// </summary>
    private static void PatchRecordRenderGraph(MethodDefinition method, PassDataFields fields, FieldDefinition swap, FieldDefinition logged, ModuleImports imports)
    {
        method.Body.SimplifyMacros();
        var instructions = method.Body.Instructions;
        var cameraData = SingleLocal(method, "UniversalCameraData");
        var resourceData = SingleLocal(method, "UniversalResourceData");
        var data = FindUnsafePassDataLocal(method);
        var captureActive = Single(instructions, i => i.OpCode == OpCodes.Stfld && IsField(i, fields.ActiveColorTexture),
            "the camera image capture in RecordRenderGraph");
        var isValid = Callee(instructions, "IsValid", "TextureHandle.IsValid in RecordRenderGraph");

        var resolveFinalTarget = imports.Field(UniversalRuntime, "UnityEngine.Rendering.Universal.UniversalCameraData", "resolveFinalTarget");
        var renderType = imports.Field(UniversalRuntime, "UnityEngine.Rendering.Universal.UniversalCameraData", "renderType");
        var setCameraColor = imports.Method(UniversalRuntime, "UnityEngine.Rendering.Universal.UniversalResourceData", "set_cameraColor",
            "UnityEngine.Rendering.RenderGraphModule.TextureHandle");
        var log = InjectedLog.Create(imports);

        var il = method.Body.GetILProcessor();
        var skip = captureActive.Next;
        Instruction[] block =
        [
            il.Create(OpCodes.Ldloc, data),
            il.Create(OpCodes.Ldflda, fields.CopyColor),
            il.Create(OpCodes.Call, isValid),
            il.Create(OpCodes.Brfalse, skip),
            il.Create(OpCodes.Ldloc, cameraData),
            il.Create(OpCodes.Ldfld, resolveFinalTarget),
            il.Create(OpCodes.Brfalse, skip),
            il.Create(OpCodes.Ldloc, cameraData),
            il.Create(OpCodes.Ldfld, renderType),
            il.Create(OpCodes.Brtrue, skip),
            il.Create(OpCodes.Ldloc, data),
            il.Create(OpCodes.Ldc_I4_1),
            il.Create(OpCodes.Stfld, swap),
            il.Create(OpCodes.Ldloc, resourceData),
            il.Create(OpCodes.Ldloc, data),
            il.Create(OpCodes.Ldfld, fields.CopyColor),
            il.Create(OpCodes.Callvirt, setCameraColor),
            il.Create(OpCodes.Ldsfld, logged),
            il.Create(OpCodes.Brtrue, skip),
            il.Create(OpCodes.Ldc_I4_1),
            il.Create(OpCodes.Stsfld, logged),
            il.Create(OpCodes.Ldstr, SwapLogMessage),
            il.Create(OpCodes.Call, log),
        ];
        InsertAfter(il, captureActive, block);
        method.Body.OptimizeMacros();
    }

    /// <summary>When the pass data is flagged, renders the bloom from the camera image straight into the copy texture and returns.</summary>
    private static void PatchExecute(MethodDefinition method, PassDataFields fields, FieldDefinition swap)
    {
        method.Body.SimplifyMacros();
        var instructions = method.Body.Instructions;
        Single(instructions, i => IsCall(i, "BlitCameraTexture"), "the camera image copy in ExecutePyramidBloomPass");
        var render = Callee(instructions, "Render", "MainEffectSO.Render in ExecutePyramidBloomPass");
        if (instructions.Count(i => IsCall(i, "Render")) != 2)
            throw new PatchTargetMismatchException("ExecutePyramidBloomPass does not render the bloom the expected way");
        var commandBuffer = StoredLocalAfter(instructions, i => IsCall(i, "GetNativeCommandBuffer"), "the command buffer");
        var mainEffectStore = Single(instructions, i => i.OpCode == OpCodes.Stloc && i.Previous is { } p && IsCall(p, "get_mainEffect"),
            "the main effect in ExecutePyramidBloomPass");

        var il = method.Body.GetILProcessor();
        var original = mainEffectStore.Next;
        Instruction[] block =
        [
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Ldfld, swap),
            il.Create(OpCodes.Brfalse, original),
            il.Create(OpCodes.Ldloc, (VariableDefinition)mainEffectStore.Operand),
            il.Create(OpCodes.Ldloc, commandBuffer),
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Ldfld, fields.ActiveColorTexture),
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Ldfld, fields.BloomTexture),
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Ldfld, fields.CopyColor),
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Ldfld, fields.TempTextures),
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Ldfld, fields.FadeValue),
            il.Create(OpCodes.Callvirt, render),
            il.Create(OpCodes.Ret),
        ];
        InsertAfter(il, mainEffectStore, block);
        method.Body.OptimizeMacros();
    }

    /// <summary>The <c>PassData</c> local that <c>RenderGraph.AddUnsafePass</c> fills in.</summary>
    private static VariableDefinition FindUnsafePassDataLocal(MethodDefinition method)
    {
        var addPass = Single(method.Body.Instructions, i => IsCall(i, "AddUnsafePass"), "RenderGraph.AddUnsafePass in RecordRenderGraph");
        for (var i = addPass.Previous; i is not null; i = i.Previous)
        {
            if (i.OpCode == OpCodes.Ldloca && i.Operand is VariableDefinition { VariableType.Name: "PassData" } local)
                return local;
            if (i.OpCode.FlowControl != FlowControl.Next)
                break;
        }
        throw new PatchTargetMismatchException("RecordRenderGraph does not pass its PassData to AddUnsafePass the expected way");
    }

    private static VariableDefinition StoredLocalAfter(IEnumerable<Instruction> instructions, Func<Instruction, bool> producer, string description)
    {
        var call = Single(instructions, producer, description);
        return call.Next is { OpCode.Code: Code.Stloc, Operand: VariableDefinition local }
            ? local
            : throw new PatchTargetMismatchException($"ExecutePyramidBloomPass does not store {description} the expected way");
    }

    /// <summary>The one method that every call named <paramref name="methodName"/> refers to.</summary>
    private static MethodReference Callee(IEnumerable<Instruction> instructions, string methodName, string description)
    {
        var callees = instructions.Where(i => IsCall(i, methodName)).Select(i => (MethodReference)i.Operand).DistinctBy(m => m.FullName).ToList();
        return callees.Count == 1 ? callees[0] : throw new PatchTargetMismatchException($"Expected exactly one {description}, found {callees.Count}");
    }

    private static VariableDefinition SingleLocal(MethodDefinition method, string typeName) =>
        Single(method.Body.Variables, v => v.VariableType.Name == typeName, $"{typeName} local in {method.Name}");

    private static T Single<T>(IEnumerable<T> items, Func<T, bool> predicate, string description)
    {
        var matches = items.Where(predicate).Take(2).ToList();
        return matches.Count == 1 ? matches[0] : throw new PatchTargetMismatchException($"Expected exactly one {description}, found {matches.Count}");
    }

    private static void InsertAfter(ILProcessor il, Instruction target, IEnumerable<Instruction> block)
    {
        foreach (var instruction in block.Reverse())
            il.InsertAfter(target, instruction);
    }

    private static MethodDefinition FindMethod(TypeDefinition type, string name) =>
        type.Methods.SingleOrDefault(m => m.Name == name && m.HasBody) ?? throw new PatchTargetMismatchException($"{type.Name}.{name} not found");

    private static bool IsCall(Instruction instruction, string methodName) =>
        (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
        && instruction.Operand is MethodReference called && called.Name == methodName;

    private static bool IsField(Instruction instruction, FieldDefinition field) =>
        instruction.Operand is FieldReference f && f.Name == field.Name && f.DeclaringType.FullName == field.DeclaringType.FullName;

    /// <summary>The <c>PassData</c> fields the patch reads, each required to exist.</summary>
    private sealed class PassDataFields(TypeDefinition passData)
    {
        public FieldDefinition ActiveColorTexture { get; } = Field(passData, "activeColorTexture");
        public FieldDefinition CopyColor { get; } = Field(passData, "copyColor");
        public FieldDefinition BloomTexture { get; } = Field(passData, "bloomTexture");
        public FieldDefinition TempTextures { get; } = Field(passData, "tempTextures");
        public FieldDefinition FadeValue { get; } = Field(passData, "fadeValue");

        private static FieldDefinition Field(TypeDefinition type, string name) =>
            type.Fields.SingleOrDefault(f => f.Name == name) ?? throw new PatchTargetMismatchException($"MainEffectPostRenderPass.PassData.{name} not found");
    }
}
