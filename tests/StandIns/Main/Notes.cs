using UnityEngine;

/// <summary>Stand-in for the game's description of how a note was cut.</summary>
public struct NoteCutInfo
{
    public bool allIsOK;
}

/// <summary>
/// Stand-in for the game's note controller. <see cref="SendNoteWasCutEvent"/> has the game's signature and is called
/// once per cut; <see cref="Cut"/> triggers it.
/// </summary>
public class NoteController : MonoBehaviour
{
    public int CutEvents { get; private set; }

    public void Cut()
    {
        var info = new NoteCutInfo { allIsOK = true };
        SendNoteWasCutEvent(in info);
    }

    protected void SendNoteWasCutEvent(in NoteCutInfo noteCutInfo) => CutEvents++;
}
