using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace WriteFluencyApi.ExternalApis.OpenAI;

[DataContract]
public record CompletionResponse {
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("object")]
    public string Object { get; set; } = null!;

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = null!;

    [JsonPropertyName("usage")]
    public Usage Usage { get; set; } = null!;

    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; } = null!;


}

public record Usage {
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public record Choice {
    [JsonPropertyName("message")]
    public ResponseMessage Message { get; set; } = null!;

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; set; } = null!;
}

public record ResponseMessage {
    [JsonPropertyName("role")]
    public string Role { get; set; } = null!;

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
}