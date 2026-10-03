using System.Reflection;
using BeatSaberFrameFixes.Patches;
using UnityEngine;

namespace BeatSaberFrameFixes.Tests;

[Collection(nameof(UnityStaticState))]
public class NoteHitTimePatchTests
{
    private static readonly Assembly Patched = StandIns.PatchAndLoad(StandIns.MainPath, NoteHitTimePatch.Apply);

    [Fact]
    public void Cutting_a_note_records_the_time()
    {
        var note = Activator.CreateInstance(Patched.GetType("NoteController", throwOnError: true)!)!;
        Time.realtimeSinceStartup = 42.5f;

        note.GetType().GetMethod("Cut")!.Invoke(note, null);

        Assert.Equal(42.5f, Shader.GetGlobalFloat(NoteHitTimePatch.GlobalName));
        Assert.Equal(1, note.GetType().GetProperty("CutEvents")!.GetValue(note));
    }

    [Fact]
    public void Missing_cut_event_is_reported_as_a_mismatch()
    {
        var module = StandIns.ReadModule(StandIns.MainPath);
        var note = module.GetType("NoteController");
        note.Methods.Remove(note.Methods.Single(m => m.Name == "SendNoteWasCutEvent"));

        Assert.Throws<PatchTargetMismatchException>(() => NoteHitTimePatch.Apply(module));
    }
}
