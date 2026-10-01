using UnityEngine;

/// <summary>Stand-in for the game's XR system event kinds.</summary>
public enum XRSystemEventType
{
    InputFocusLost,
    InputFocusAcquired,
    VRFocusLost,
    VRFocusAcquired,
    HmdMounted,
    HmdUnmounted,
    ControllersDidChangeReference,
    ControllersDidDisconnect,
}

/// <summary>Stand-in for the game's XR focus and presence state.</summary>
public interface IXRSystemState
{
    bool hasInputFocus { get; }

    bool hasHmdMounted { get; }

    bool IsAppFocusCurrentlyLost();
}

/// <summary>Stand-in for the game's OpenXR-backed state, with settable focus and presence.</summary>
public class UnityXRSystemState : IXRSystemState
{
    public bool hasInputFocus { get; set; } = true;

    public bool hasHmdMounted { get; set; } = true;

    public bool IsAppFocusCurrentlyLost() => !hasInputFocus || !hasHmdMounted;
}

/// <summary>
/// Stand-in for the game's pause controller. <see cref="HandleSystemStateChange"/> has the same pause logic as the
/// game's method; pauses are counted in <see cref="PauseCount"/>.
/// </summary>
public class PauseController : MonoBehaviour
{
    private readonly IXRSystemState _xrSystemState;

    public PauseController(IXRSystemState xrSystemState)
    {
        _xrSystemState = xrSystemState;
    }

    /// <summary>Simulates the game's FPFC (desktop, no headset) launch option, which disables automatic pauses.</summary>
    public bool FpfcEnabled { get; set; }

    /// <summary>Number of times <see cref="Pause"/> has been called.</summary>
    public int PauseCount { get; private set; }

    public void Pause() => PauseCount++;

    private void HandleSystemStateChange(XRSystemEventType eventType)
    {
        switch (eventType)
        {
            case XRSystemEventType.InputFocusLost:
                if (!HadFpfcEnabledAtInit())
                {
                    Pause();
                }
                break;
            case XRSystemEventType.HmdUnmounted:
                if (!HadFpfcEnabledAtInit())
                {
                    Pause();
                }
                break;
        }
    }

    private bool HadFpfcEnabledAtInit() => FpfcEnabled;
}
