using UnityEngine;
using UnityEngine.XR;

namespace BeatSaber.Haptics;

/// <summary>Stand-in for the game's haptic preset asset, with the same serialized fields.</summary>
public class HapticPresetSO : ScriptableObject
{
    /// <summary>Pulse length in seconds; unused by continuous presets.</summary>
    public float _duration = 0.05f;

    /// <summary>Pulse amplitude, 0..1 as sent to the controller.</summary>
    public float _strength = 1f;

    public float _frequency = 0.5f;

    /// <summary>True for rumble that lasts while a saber touches something, re-triggered every frame.</summary>
    public bool _continuous;
}

/// <summary>Stand-in for the game's per-preset rumble state.</summary>
public class RumbleData
{
    public bool active;

    public bool continuous;

    public float strength;

    public float endTime;

    public float frequency;
}

/// <summary>
/// Stand-in for the game's rumble player. <see cref="PlayHapticFeedback"/> has the same statements as the game's
/// method; the resulting rumble is exposed through <see cref="LastRumble"/>.
/// </summary>
public class RumbleHapticFeedbackPlayer : MonoBehaviour
{
    /// <summary>The rumble written by the most recent <see cref="PlayHapticFeedback"/> call.</summary>
    public RumbleData LastRumble { get; } = new();

    public void PlayHapticFeedback(XRNode node, HapticPresetSO hapticPreset)
    {
        RumbleData rumble = LastRumble;
        if (rumble != null && hapticPreset != null)
        {
            rumble.active = true;
            rumble.continuous = hapticPreset._continuous;
            rumble.strength = hapticPreset._strength;
            rumble.endTime = Time.time + hapticPreset._duration;
            rumble.frequency = hapticPreset._frequency;
        }
    }

    public bool CanPlayHapticPreset(HapticPresetSO hapticPreset, XRNode node)
    {
        return hapticPreset._duration > 0f && hapticPreset._frequency > 0f && hapticPreset._strength > 0f;
    }
}
