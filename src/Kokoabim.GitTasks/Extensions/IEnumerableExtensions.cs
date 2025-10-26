namespace Kokoabim.GitTasks;

public static class IEnumerableExtensions
{
    public static string? CombineNonNull<T>(this IEnumerable<T?> source, string delimiter) where T : class
    {
        var items = source.NonNull();
        return items is null ? null : string.Join<T>(delimiter, items);
    }

    public static string? CombineNonNullOrWhiteSpace(this IEnumerable<string?> source, string delimiter)
    {
        var items = source.NonNullOrWhiteSpace();
        return items is null ? null : string.Join(delimiter, items);
    }

    public static T[] ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (var item in source) action(item);

        return [.. source];
    }

    public static T[]? NonNull<T>(this IEnumerable<T?> source) where T : class
    {
        var items = source.Where(i => i is not null);
        return items.Any() ? [.. items!] : null;
    }

    public static string[]? NonNullOrWhiteSpace(this IEnumerable<string?> source)
    {
        var items = source.Where(i => !string.IsNullOrWhiteSpace(i));
        return items.Any() ? [.. items!] : null;
    }
}