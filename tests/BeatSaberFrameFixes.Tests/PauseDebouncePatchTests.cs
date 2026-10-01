using System.Reflection;
using BeatSaberFrameFixes.Patches;
using Mono.Cecil;

namespace BeatSaberFrameFixes.Tests;

[Collection(nameof(UnityStaticState))]
public class PauseDebouncePatchTests
{
    private const int DebounceMilliseconds = 250;

    [Fact]
    public void Short_focus_loss_does_not_pause_and_is_logged()
    {
        var game = new PausingGame();

        game.Lose("InputFocusLost", at: 5f);
        game.Regain(at: 5.02f);
        game.Update();

        Assert.Equal(0, game.PauseCount);
        Assert.Contains(UnityEngine.Debug.Messages, m => m.ToString()!.Contains("Ignored focus/presence blip of 20"));
    }

    [Fact]
    public void Loss_that_lasts_past_the_debounce_pauses_once()
    {
        var game = new PausingGame();

        game.Lose("HmdUnmounted", at: 5f);
        game.Update(at: 5.1f);
        Assert.Equal(0, game.PauseCount);

        game.Update(at: 5.3f);
        game.Update(at: 5.4f);
        Assert.Equal(1, game.PauseCount);
        Assert.Contains(UnityEngine.Debug.Messages, m => m.ToString()!.Contains("lost for over 250 ms, pausing"));
    }

    [Fact]
    public void Focus_and_presence_lost_together_count_as_one_loss()
    {
        var game = new PausingGame();

        game.Lose("HmdUnmounted", at: 5f);
        game.Lose("InputFocusLost", at: 5.2f);
        game.Update(at: 5.26f);

        Assert.Equal(1, game.PauseCount);
    }

    [Fact]
    public void Fpfc_mode_never_pauses_on_focus_loss()
    {
        var game = new PausingGame { FpfcEnabled = true };

        game.Lose("InputFocusLost", at: 5f);
        game.Update(at: 10f);

        Assert.Equal(0, game.PauseCount);
    }

    [Fact]
    public void Other_events_do_not_start_a_debounce()
    {
        var game = new PausingGame();

        game.Send("HmdMounted", at: 5f);
        game.SetFocus(false);
        game.Update(at: 10f);

        Assert.Equal(0, game.PauseCount);
    }

    [Fact]
    public void Existing_update_method_is_reported_as_a_mismatch()
    {
        var module = StandIns.ReadModule(StandIns.MainPath);
        var controller = module.GetType("PauseController");
        controller.Methods.Add(new MethodDefinition("Update", Mono.Cecil.MethodAttributes.Private, module.TypeSystem.Void));

        Assert.Throws<PatchTargetMismatchException>(() => PauseDebouncePatch.Apply(module, DebounceMilliseconds));
    }

    [Fact]
    public void Missing_pause_call_is_reported_as_a_mismatch()
    {
        var module = StandIns.ReadModule(StandIns.MainPath);
        PauseDebouncePatch.Apply(module, DebounceMilliseconds);

        Assert.Throws<PatchTargetMismatchException>(() => PauseDebouncePatch.Apply(module, DebounceMilliseconds));
    }

    /// <summary>A patched stand-in pause controller with its XR state, driven through reflection.</summary>
    private sealed class PausingGame
    {
        private static readonly Assembly Patched = StandIns.PatchAndLoad(StandIns.MainPath, m => PauseDebouncePatch.Apply(m, DebounceMilliseconds));

        private readonly object _state;
        private readonly object _controller;
        private readonly Type _controllerType;

        public PausingGame()
        {
            UnityEngine.Debug.Messages.Clear();
            _state = Activator.CreateInstance(Patched.GetType("UnityXRSystemState", throwOnError: true)!)!;
            _controllerType = Patched.GetType("PauseController", throwOnError: true)!;
            _controller = Activator.CreateInstance(_controllerType, _state)!;
        }

        public bool FpfcEnabled
        {
            set => _controllerType.GetProperty("FpfcEnabled")!.SetValue(_controller, value);
        }

        public int PauseCount => (int)_controllerType.GetProperty("PauseCount")!.GetValue(_controller)!;

        public void Lose(string eventName, float at)
        {
            SetFocus(false);
            Send(eventName, at);
        }

        public void Regain(float at)
        {
            SetFocus(true);
            UnityEngine.Time.realtimeSinceStartup = at;
        }

        public void SetFocus(bool focused) => _state.GetType().GetProperty("hasInputFocus")!.SetValue(_state, focused);

        public void Send(string eventName, float at)
        {
            UnityEngine.Time.realtimeSinceStartup = at;
            var eventType = Patched.GetType("XRSystemEventType", throwOnError: true)!;
            Invoke("HandleSystemStateChange", Enum.Parse(eventType, eventName));
        }

        public void Update(float? at = null)
        {
            if (at is { } time)
                UnityEngine.Time.realtimeSinceStartup = time;
            Invoke("Update");
        }

        private void Invoke(string method, params object[] args) =>
            _controllerType.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(_controller, args);
    }
}

/// <summary>Serializes tests that use the stand-in UnityEngine's static clock and log.</summary>
[CollectionDefinition(nameof(UnityStaticState), DisableParallelization = true)]
public class UnityStaticState;
