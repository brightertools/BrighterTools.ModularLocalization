using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;

namespace BrighterTools.ModularLocalization.Internal;

internal static class PlaceholderFormatter
{
    private static readonly Regex TokenRegex = new(@"\{(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\}",
        RegexOptions.Compiled);

    private static readonly ConcurrentDictionary<Type, Dictionary<string, Func<object, object?>>> AccessorCache = new();

    public static string Format(string template, object values)
    {
        if (string.IsNullOrEmpty(template)) return template;
        if (values is null) return template;

        var accessors = GetAccessors(values.GetType());

        return TokenRegex.Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            if (!accessors.TryGetValue(name, out var getter)) return m.Value;

            var v = getter(values);
            return v?.ToString() ?? string.Empty;
        });
    }

    private static Dictionary<string, Func<object, object?>> GetAccessors(Type type)
    {
        return AccessorCache.GetOrAdd(type, t =>
        {
            var dict = new Dictionary<string, Func<object, object?>>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!p.CanRead) continue;

                var getMethod = p.GetGetMethod();
                if (getMethod == null) continue;

                dict[p.Name] = (obj) => p.GetValue(obj);
            }

            return dict;
        });
    }
}