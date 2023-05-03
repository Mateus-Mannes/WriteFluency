
using System.Runtime.Serialization;

namespace WriteFluencyApi.ExternalApis.OpenAI.Requests;

[DataContract]
public record CompletionRequest
{
    [DataMember(Name = "model")]
    public string Model { get; set; } = null!;

    [DataMember(Name = "prompt")]
    public string Prompt { get; set; } = null!;

    [DataMember(Name = "max_tokens")]
    public int MaxTokens { get; set; }

    [DataMember(Name = "temperature")]
    public decimal Temperature { get; set; }
}
