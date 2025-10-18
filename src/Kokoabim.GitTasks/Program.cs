using Kokoabim.CommandLineInterface;
using Kokoabim.GitTasks;

var consoleApp = new ConsoleApp(titleText: "Git Tasks", commands: new Commands().Generate())
{
    DefaultCommandName = Tasks.DefaultCommandName
};

return await consoleApp.RunAsync(args);