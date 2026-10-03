using UnityEngine;

/// <summary>Stand-in for the game's audio time source interface and its song state.</summary>
public interface IAudioTimeSource
{
    public enum State
    {
        Stopped,
        Playing,
        Paused,
        Failed,
    }
}

/// <summary>
/// Stand-in for the game's song clock. The state methods follow the game's; <see cref="Update"/> and
/// <see cref="OnDestroy"/> exist so patches can hook them.
/// </summary>
public class AudioTimeSyncController : MonoBehaviour
{
    private IAudioTimeSource.State _state = IAudioTimeSource.State.Stopped;

    private float _songTime;

    public void StartSong() => _state = IAudioTimeSource.State.Playing;

    public void Pause()
    {
        if (_state == IAudioTimeSource.State.Playing)
            _state = IAudioTimeSource.State.Paused;
    }

    public void Resume()
    {
        if (_state == IAudioTimeSource.State.Paused)
            _state = IAudioTimeSource.State.Playing;
    }

    protected void Update()
    {
        if (_state == IAudioTimeSource.State.Stopped)
            return;
        _songTime += Time.time;
    }

    protected void OnDestroy()
    {
        _songTime = 0f;
    }
}
