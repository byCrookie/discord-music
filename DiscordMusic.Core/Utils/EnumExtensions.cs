using System.Reflection;
using System.Runtime.Serialization;

namespace DiscordMusic.Core.Utils;

public static class EnumExtensions
{
    public static string GetEnumMemberValue<T>(this T value) where T : IConvertible
    {
        return value.GetType().GetTypeInfo().DeclaredMembers
            .Single(x => x.Name == value.ToString())
            .GetCustomAttribute<EnumMemberAttribute>(false)
            ?.Value ?? value.ToString() ?? throw new ArgumentException($"Enum value {value} not found.");
    }
}