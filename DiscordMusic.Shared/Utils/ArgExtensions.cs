namespace DiscordMusic.Shared.Utils;

public static class ArgExtensions
{
    public static T? GetArgValue<T>(
        this IEnumerable<string> args,
        string name,
        T? defaultValue = default)
    {
        var arguments = args.ToList();
        
        var argumentIndex = arguments.FindIndex(a => a.StartsWith(name));
        if (argumentIndex == -1)
        {
            return defaultValue;
        }

        var argument = arguments.Count > argumentIndex + 1 ? arguments[argumentIndex + 1] : null;
        if (argument is null)
        {
            return defaultValue;
        }

        try
        {
            if (typeof(T).IsEnum)
            {
                return (T)Enum.Parse(typeof(T), argument, true);
            }
            
            return (T)Convert.ChangeType(argument, typeof(T));
        }
        catch (Exception e)
        {
            throw new ArgumentException($"Could not convert argument '{argument}' to type '{typeof(T)}'", e);
        }
    }
}