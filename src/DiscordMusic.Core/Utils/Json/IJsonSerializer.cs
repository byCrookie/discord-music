namespace DiscordMusic.Core.Utils.Json;

public interface IJsonSerializer
{
    string Serialize<T>(T obj);
    T? Deserialize<T>(string json);
}