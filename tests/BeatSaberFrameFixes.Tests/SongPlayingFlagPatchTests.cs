using System.Reflection;
using BeatSaberFrameFixes.Patches;
using Mono.Cecil;
using UnityEngine;

namespace BeatSaberFrameFixes.Tests;

[Collection(nameof(UnityStaticState))]
public class SongPlayingFlagPatchTests
{
    private static readonly Assembly Patched = StandIns.PatchAndLoad(StandIns.MainPath, SongPlayingFlagPatch.Apply);

    [Fact]
    public void Flag_follows_the_song_state_each_frame()
    {
        var clock = new SongClock();

        clock.Update();
        Assert.Equal(0f, Flag);

        clock.Call("StartSong");
        clock.Update();
        Assert.Equal(1f, Flag);

        clock.Call("Pause");
        clock.Update();
        Assert.Equal(0f, Flag);

        clock.Call("Resume");
        clock.Update();
        Assert.Equal(1f, Flag);
    }

    [Fact]
    public void Destroying_the_clock_clears_the_flag()
    {
        var clock = new SongClock();
        clock.Call("StartSong");
        clock.Update();

        clock.Call("OnDestroy");

        Assert.Equal(0f, Flag);
    }

    [Fact]
    public void Missing_update_is_reported_as_a_mismatch()
    {
        var module = StandIns.ReadModule(StandIns.MainPath);
        var controller = module.GetType("AudioTimeSyncController");
        controller.Methods.Remove(controller.Methods.Single(m => m.Name == "Update"));

        Assert.Throws<PatchTargetMismatchException>(() => SongPlayingFlagPatch.Apply(module));
    }

    private static float Flag => Shader.GetGlobalFloat(SongPlayingFlagPatch.GlobalName);

    /// <summary>A patched stand-in song clock, driven through reflection.</summary>
    private sealed class SongClock
    {
        private readonly object _clock;

        public SongClock()
        {
            Shader.Globals.Clear();
            _clock = Activator.CreateInstance(Patched.GetType("AudioTimeSyncController", throwOnError: true)!)!;
        }

        public void Update() => Call("Update");

        public void Call(string method) =>
            _clock.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(_clock, []);
    }
}
