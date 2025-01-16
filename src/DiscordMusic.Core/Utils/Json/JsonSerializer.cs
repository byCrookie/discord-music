using System.Text.Json;

namespace DiscordMusic.Core.Utils.Json;

internal class JsonSerializer : IJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        Converters = { new FlurlUrlJsonConverter() }
    };

    public string Serialize<T>(T obj)
    {
        return System.Text.Json.JsonSerializer.Serialize(obj, Options);
    }

    public T? Deserialize<T>(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }
}