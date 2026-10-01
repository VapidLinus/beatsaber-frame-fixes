namespace BeatSaberFrameFixes;

/// <summary>A Beat Saber installation folder and the game version from its <c>BeatSaberVersion.txt</c>, e.g. <c>1.45.1_27839</c>.</summary>
internal sealed record GameInstall(string Directory, string Version)
{
    public string ManagedDirectory => Path.Combine(Directory, "Beat Saber_Data", "Managed");

    /// <summary>The version without its build suffix, e.g. <c>1.45.1</c>.</summary>
    public string DisplayVersion => Version.Split('_')[0];
}
