namespace Kokoabim.GitTasks;

public static class IEnumerableExtensions
{
    public static string? CombineNonNulls<T>(this IEnumerable<T?> source, string delimiter) where T : class
    {
        var items = source.NonNulls();
        return items is null ? null : string.Join<T>(delimiter, items);
    }

    public static T[] ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source) action(item);

        return [.. source];
    }

    public static T[]? NonNulls<T>(this IEnumerable<T?> source) where T : class
    {
        var items = source.Where(i => i is not null);
        return items.Any() ? [.. items!] : null;
    }
}