namespace Kokoabim.GitTasks;

public static class IEnumerableExtensions
{
    public static T[] ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source) action(item);

        return [.. source];
    }
}