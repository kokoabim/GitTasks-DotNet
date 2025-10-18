namespace Kokoabim.GitTasks.Tests;

public class GitTests
{
    [Fact]
    public async Task GetVersionAsync_ReturnsVersion_WhenGitIsInstalledAsync()
    {
        // Arrange
        var git = new Git();

        // Act
        var result = await git.GetVersionAsync();

        // Assert
        Assert.StartsWith("git version", result);
    }

    [Fact]
    public void IsInstalled_ReturnsTrue_WhenGitIsInstalled()
    {
        // Arrange
        var git = new Git();

        // Act
        var result = git.IsInstalled;

        // Assert
        Assert.True(result);
    }
}