using Kokoabim.CommandLineInterface;
using Kokoabim.GitTasks;

var consoleApp = new ConsoleApp(titleText: "Git Tasks", commands: GitTasksCommands.Create())
{
    DefaultCommandName = GitTasksCommandOperations.DefaultCommandName,
    GlobalArguments = GitTasksArguments.Globals,
};

return await consoleApp.RunAsync(args);