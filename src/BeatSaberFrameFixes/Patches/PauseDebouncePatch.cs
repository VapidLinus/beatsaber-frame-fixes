using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Debounces the game's automatic pause on OpenXR input focus loss and headset removal. In
/// <c>PauseController.HandleSystemStateChange</c> each <c>Pause()</c> call is redirected to a method that starts a
/// timer; an injected <c>PauseController.Update</c> pauses only if focus or presence is still lost once the timer
/// exceeds the debounce. Shorter losses are skipped and logged to Unity's Player.log. All other pause routes
/// (pause button, application pause, level start/resume) are unchanged.
/// </summary>
internal static class PauseDebouncePatch
{
    public const string FileName = "Main.dll";

    private const string PendingFieldName = "_frameFixesLossPending";
    private const string LossTimeFieldName = "_frameFixesLossTime";

    public static void Apply(ModuleDefinition module, int debounceMilliseconds)
    {
        var imports = new ModuleImports(module);
        var controller = module.GetType("PauseController") ?? throw new PatchTargetMismatchException("PauseController not found");
        var handler = FindMethod(controller, "HandleSystemStateChange", parameterCount: 1);
        var pause = FindMethod(controller, "Pause", parameterCount: 0);
        if (controller.Methods.Any(m => m.Name == "Update"))
            throw new PatchTargetMismatchException("PauseController already has an Update method");
        var pauseCalls = handler.Body.Instructions.Where(i => IsCallTo(i, pause)).ToList();
        if (pauseCalls.Count == 0)
            throw new PatchTargetMismatchException("PauseController.HandleSystemStateChange no longer calls Pause()");

        var xrState = controller.Fields.SingleOrDefault(f => f.Name == "_xrSystemState")
            ?? throw new PatchTargetMismatchException("PauseController._xrSystemState not found");
        var isFocusLost = FindMethod(xrState.FieldType.Resolve(), "IsAppFocusCurrentlyLost", parameterCount: 0);

        var pending = new FieldDefinition(PendingFieldName, FieldAttributes.Private, module.TypeSystem.Boolean);
        var lossTime = new FieldDefinition(LossTimeFieldName, FieldAttributes.Private, module.TypeSystem.Single);
        controller.Fields.Add(pending);
        controller.Fields.Add(lossTime);

        var realtime = imports.Unity("UnityEngine.Time", "get_realtimeSinceStartup");
        var log = InjectedLog.Create(imports);
        var beginDebounce = CreateBeginDebounce(module, pending, lossTime, realtime);
        controller.Methods.Add(beginDebounce);
        controller.Methods.Add(CreateUpdate(imports, debounceMilliseconds, pending, lossTime, realtime, log, module.ImportReference(isFocusLost), xrState, pause));

        var il = handler.Body.GetILProcessor();
        foreach (var call in pauseCalls)
            il.Replace(call, il.Create(OpCodes.Call, beginDebounce));
    }

    /// <summary>Creates <c>BeginPauseDebounce()</c>: records when focus or presence was first lost, unless a debounce is already running.</summary>
    private static MethodDefinition CreateBeginDebounce(ModuleDefinition module, FieldDefinition pending, FieldDefinition lossTime, MethodReference realtime)
    {
        var method = new MethodDefinition("BeginPauseDebounce", MethodAttributes.Private | MethodAttributes.HideBySig, module.TypeSystem.Void);
        var il = method.Body.GetILProcessor();
        var ret = il.Create(OpCodes.Ret);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, pending);
        il.Emit(OpCodes.Brtrue, ret);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stfld, pending);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, realtime);
        il.Emit(OpCodes.Stfld, lossTime);
        il.Append(ret);
        return method;
    }

    /// <summary>
    /// Creates <c>Update()</c>: while a debounce runs, logs and ends it if focus and presence came back, or pauses once
    /// the loss has lasted the debounce time.
    /// </summary>
    private static MethodDefinition CreateUpdate(ModuleImports imports, int debounceMilliseconds, FieldDefinition pending, FieldDefinition lossTime,
        MethodReference realtime, MethodReference log, MethodReference isFocusLost, FieldDefinition xrState, MethodDefinition pause)
    {
        var module = imports.Module;
        var concat3 = imports.Corlib("System.String", "Concat", "System.String", "System.String", "System.String");
        var objectToString = imports.Corlib("System.Object", "ToString");
        var int32 = imports.CorlibType("System.Int32");

        var method = new MethodDefinition("Update", MethodAttributes.Private | MethodAttributes.HideBySig, module.TypeSystem.Void);
        var il = method.Body.GetILProcessor();
        var ret = il.Create(OpCodes.Ret);
        var stillLost = il.Create(OpCodes.Call, realtime);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, pending);
        il.Emit(OpCodes.Brfalse, ret);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, xrState);
        il.Emit(OpCodes.Callvirt, isFocusLost);
        il.Emit(OpCodes.Brtrue, stillLost);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stfld, pending);
        il.Emit(OpCodes.Ldstr, "Ignored focus/presence blip of ");
        il.Emit(OpCodes.Call, realtime);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, lossTime);
        il.Emit(OpCodes.Sub);
        il.Emit(OpCodes.Ldc_R4, 1000f);
        il.Emit(OpCodes.Mul);
        il.Emit(OpCodes.Ldc_R4, 0.5f);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Box, int32);
        il.Emit(OpCodes.Callvirt, objectToString);
        il.Emit(OpCodes.Ldstr, " ms");
        il.Emit(OpCodes.Call, concat3);
        il.Emit(OpCodes.Call, log);
        il.Emit(OpCodes.Ret);

        il.Append(stillLost);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, lossTime);
        il.Emit(OpCodes.Sub);
        il.Emit(OpCodes.Ldc_R4, debounceMilliseconds / 1000f);
        il.Emit(OpCodes.Blt_Un, ret);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Stfld, pending);
        il.Emit(OpCodes.Ldstr, $"Focus/presence lost for over {debounceMilliseconds} ms, pausing");
        il.Emit(OpCodes.Call, log);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, pause);
        il.Append(ret);
        return method;
    }

    private static MethodDefinition FindMethod(TypeDefinition type, string name, int parameterCount) =>
        type.Methods.SingleOrDefault(m => m.Name == name && m.Parameters.Count == parameterCount)
        ?? throw new PatchTargetMismatchException($"{type.Name}.{name} not found");

    private static bool IsCallTo(Instruction instruction, MethodDefinition target) =>
        (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
        && instruction.Operand is MethodReference called
        && called.Name == target.Name
        && called.DeclaringType.FullName == target.DeclaringType.FullName
        && called.Parameters.Count == target.Parameters.Count;
}
