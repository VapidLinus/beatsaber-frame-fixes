using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BeatSaberFrameFixes.Patches;

/// <summary>
/// Injects <c>BeatSaberFrameFixes.Log.Write(string)</c> into a game module. It writes
/// <c>[beatsaber-frame-fixes] HH:mm:ss.fff message</c> through <c>UnityEngine.Debug.Log</c>, which lands in Player.log.
/// </summary>
internal static class InjectedLog
{
    public const string Prefix = "[beatsaber-frame-fixes] ";

    public static MethodReference Create(ModuleImports imports)
    {
        var module = imports.Module;
        var type = new TypeDefinition("BeatSaberFrameFixes", "Log",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit,
            module.TypeSystem.Object);
        module.Types.Add(type);

        var write = new MethodDefinition("Write", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, module.TypeSystem.Void);
        write.Parameters.Add(new ParameterDefinition("message", ParameterAttributes.None, module.TypeSystem.String));
        var now = new VariableDefinition(imports.CorlibType("System.DateTime"));
        write.Body.Variables.Add(now);
        write.Body.InitLocals = true;

        var il = write.Body.GetILProcessor();
        il.Emit(OpCodes.Call, imports.Corlib("System.DateTime", "get_Now"));
        il.Emit(OpCodes.Stloc, now);
        il.Emit(OpCodes.Ldstr, Prefix);
        il.Emit(OpCodes.Ldloca, now);
        il.Emit(OpCodes.Ldstr, "HH:mm:ss.fff");
        il.Emit(OpCodes.Call, imports.Corlib("System.DateTime", "ToString", "System.String"));
        il.Emit(OpCodes.Ldstr, " ");
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, imports.Corlib("System.String", "Concat", "System.String", "System.String", "System.String", "System.String"));
        il.Emit(OpCodes.Call, imports.Unity("UnityEngine.Debug", "Log", "System.Object"));
        il.Emit(OpCodes.Ret);
        type.Methods.Add(write);
        return write;
    }
}
