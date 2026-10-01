namespace BeatSaberFrameFixes.Tests;

public class PatchMarkerTests
{
    [Fact]
    public void Unpatched_module_has_no_marker()
    {
        using var module = StandIns.ReadModule(StandIns.MainPath);

        Assert.Null(PatchMarker.Read(module));
    }

    [Fact]
    public void Added_marker_is_read_back_after_a_round_trip()
    {
        var stream = new MemoryStream();
        using (var module = StandIns.ReadModule(StandIns.MainPath))
        {
            PatchMarker.Add(module, new PatchMarker.Info("pause debounce 250 ms", "ab12"));
            module.Write(stream);
        }
        stream.Position = 0;

        using var reread = Mono.Cecil.ModuleDefinition.ReadModule(stream);
        Assert.Equal(new PatchMarker.Info("pause debounce 250 ms", "ab12"), PatchMarker.Read(reread));
    }
}
