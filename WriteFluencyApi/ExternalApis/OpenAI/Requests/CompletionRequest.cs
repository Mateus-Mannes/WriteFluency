
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace WriteFluencyApi.ExternalApis.OpenAI.Requests;

public record CompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = null!;

    [JsonPropertyName("messages")]
    public List<Message> Messages { get; set; } = null!;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public decimal Temperature { get; set; }
}

[DataContract]
public record Message {
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
}
