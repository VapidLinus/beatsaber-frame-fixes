namespace BeatSaberFrameFixes.Tests;

public class GameProcessTests
{
    [Theory]
    [InlineData("Z:\\home\\steamos\\.local\\share\\Steam\\steamapps\\common\\Beat Saber\\Beat Saber.exe\0")]
    [InlineData("/usr/bin/python3\0/home/steamos/proton\0waitforexitandrun\0/home/steamos/Beat Saber/Beat Saber.exe\0")]
    [InlineData("C:\\BEAT SABER\\beat saber.EXE")]
    public void Recognizes_the_game_executable_as_an_argument(string commandLine)
    {
        Assert.True(GameProcess.IsGameCommandLine(commandLine));
    }

    [Theory]
    [InlineData("bash\0-c\0pgrep -af 'Beat Saber.exe' && echo found\0")]
    [InlineData("kate\0/home/steamos/notes about Beat Saber.exe.txt\0")]
    [InlineData("")]
    public void Ignores_processes_that_only_mention_the_game(string commandLine)
    {
        Assert.False(GameProcess.IsGameCommandLine(commandLine));
    }
}
