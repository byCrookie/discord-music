namespace DiscordMusic.Core.Utils.Json;

internal interface IJsonSerializer
{
    string Serialize<T>(T obj);
    T? Deserialize<T>(string json);
}