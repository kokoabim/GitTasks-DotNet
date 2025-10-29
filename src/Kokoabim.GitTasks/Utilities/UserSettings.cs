using System.Diagnostics.CodeAnalysis;

namespace Kokoabim.GitTasks;

public class UserSettings
{
    public IReadOnlyCollection<string[]> LinkedAuthorEmails { get; set; } = [];
    public IReadOnlyCollection<string[]> RenamedAuthorNames { get; set; } = [];

    private Dictionary<string, string>? _authorEmailsMapping;
    private Dictionary<string, string>? _authorNamesMapping;
    private static readonly FileSystem _fileSystem = new();

    public void ChangeAuthorNames(IEnumerable<GitLogEntry> gitLogEntries)
    {
        if (_authorNamesMapping == null)
        {
            _authorNamesMapping = [];

            foreach (var nameGroup in RenamedAuthorNames)
            {
                if (nameGroup.Length < 2) continue;
                var primaryName = nameGroup[0];
                foreach (var name in nameGroup.Skip(1)) _authorNamesMapping[name] = primaryName;
            }
        }

        foreach (var logEntry in gitLogEntries)
        {
            if (_authorNamesMapping.TryGetValue(logEntry.AuthorName, out var changedName)) logEntry.AuthorName = changedName;
        }
    }

    public void LinkAuthorEmails(IEnumerable<GitLogEntry> gitLogEntries)
    {
        if (_authorEmailsMapping == null)
        {
            _authorEmailsMapping = [];

            foreach (var emailGroup in LinkedAuthorEmails)
            {
                if (emailGroup.Length < 2) continue;
                var primaryEmail = emailGroup[0].ToLower();
                foreach (var email in emailGroup.Skip(1)) _authorEmailsMapping[email.ToLower()] = primaryEmail;
            }
        }

        foreach (var logEntry in gitLogEntries)
        {
            if (_authorEmailsMapping.TryGetValue(logEntry.AuthorEmail, out var linkedEmail)) logEntry.AuthorEmail = linkedEmail;
        }
    }

    public static bool TryLoad([NotNullWhen(true)] out UserSettings? userSettings, string? path = null)
    {
        userSettings = path is null ?
            _fileSystem.ReadFile<UserSettings>(_fileSystem.GetUserHomePath(), ".git-tasks.json")
            : _fileSystem.ReadFile<UserSettings>(path);

        return userSettings != null;
    }
}