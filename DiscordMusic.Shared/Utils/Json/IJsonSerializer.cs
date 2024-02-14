namespace DiscordMusic.Shared.Utils.Json;

public interface IJsonSerializer
{
    string Serialize<T>(T obj);
    T? Deserialize<T>(string json);
}
