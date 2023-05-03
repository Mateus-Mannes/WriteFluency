using System.Runtime.Serialization;

namespace WriteFluencyApi.ExternalApis.OpenAI.Responses;

[DataContract]
public record CompletionResponse {
    [DataMember(Name = "id")]
    public string Id { get; set; } = null!;

    [DataMember(Name = "object")]
    public string Object { get; set; } = null!;

    [DataMember(Name = "created")]
    public long Created { get; set; }

    [DataMember(Name = "model")]
    public string Model { get; set; } = null!;

    [DataMember(Name = "usage")]
    public Usage Usage { get; set; } = null!;

    [DataMember(Name = "choices")]
    public List<Choice> Choices { get; set; } = null!;


}

public record Usage {
    [DataMember(Name = "prompt_tokens")]
    public int PromptTokens { get; set; }

    [DataMember(Name = "completion_tokens")]
    public int CompletionTokens { get; set; }

    [DataMember(Name = "total_tokens")]
    public int TotalTokens { get; set; }
}

public record Choice {
    [DataMember(Name = "message")]
    public Message Message { get; set; } = null!;

    [DataMember(Name = "index")]
    public int Index { get; set; }

    [DataMember(Name = "finish_reason")]
    public string FinishReason { get; set; } = null!;
}

public record Message {
    [DataMember(Name = "role")]
    public string Role { get; set; } = null!;

    [DataMember(Name = "content")]
    public string Content { get; set; } = null!;
}