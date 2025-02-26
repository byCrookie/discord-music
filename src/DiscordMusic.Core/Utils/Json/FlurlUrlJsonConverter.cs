using System.Text.Json;
using System.Text.Json.Serialization;
using Flurl;

namespace DiscordMusic.Core.Utils.Json;

public class FlurlUrlJsonConverter : JsonConverter<Url>
{
    public override Url Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var urlString = reader.GetString();
            return string.IsNullOrWhiteSpace(urlString) ? new Url() : new Url(urlString);
        }

        throw new JsonException($"Unexpected token type {reader.TokenType} when parsing a Flurl.Url.");
    }

    public override void Write(Utf8JsonWriter writer, Url value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
