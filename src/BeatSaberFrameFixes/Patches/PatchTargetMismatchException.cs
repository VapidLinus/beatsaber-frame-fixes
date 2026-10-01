namespace BeatSaberFrameFixes.Patches;

/// <summary>Thrown when game code does not have the shape a patch expects, typically after a game update.</summary>
internal sealed class PatchTargetMismatchException(string message) : Exception(message);
