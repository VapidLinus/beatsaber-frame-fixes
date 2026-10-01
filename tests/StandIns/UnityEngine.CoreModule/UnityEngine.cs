// Minimal stand-ins for the UnityEngine.CoreModule members that patched game code calls.
// The members mirror Unity's names and signatures; their behavior is test-controllable.

namespace UnityEngine
{
    /// <summary>Stand-in for <c>UnityEngine.Object</c>, exposing the asset name.</summary>
    public class Object
    {
        /// <summary>Asset name, as Unity reports it for a ScriptableObject.</summary>
        public string name { get; set; } = "";
    }

    /// <summary>Stand-in for <c>UnityEngine.ScriptableObject</c>.</summary>
    public class ScriptableObject : Object
    {
    }

    /// <summary>Stand-in for <c>UnityEngine.MonoBehaviour</c>.</summary>
    public class MonoBehaviour : Object
    {
    }

    /// <summary>Stand-in for <c>UnityEngine.Debug</c> that records logged messages.</summary>
    public static class Debug
    {
        /// <summary>Every message passed to <see cref="Log"/>, in order.</summary>
        public static List<object> Messages { get; } = [];

        public static void Log(object message) => Messages.Add(message);
    }

    /// <summary>Stand-in for <c>UnityEngine.Time</c> with settable clocks, in seconds.</summary>
    public static class Time
    {
        public static float time { get; set; }

        public static float realtimeSinceStartup { get; set; }
    }
}

namespace UnityEngine.XR
{
    /// <summary>Stand-in for <c>UnityEngine.XR.XRNode</c>.</summary>
    public enum XRNode
    {
        Head = 3,
        LeftHand = 4,
        RightHand = 5,
    }
}
