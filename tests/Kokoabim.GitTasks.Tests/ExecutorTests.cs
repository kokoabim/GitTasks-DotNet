namespace Kokoabim.GitTasks.Tests;

public class ExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess()
    {
        // Arrange
        var executor = new Executor();

        // Act
        var result = await executor.ExecuteAsync("git", arguments: "--version");

        // Assert
        Assert.True(result.Success);
    }
}