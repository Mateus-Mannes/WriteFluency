using System.Text.Json.Serialization;

namespace WriteFluency.Propositions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubjectEnum
{
    General,
    Science,
    Sports,
    Business,
    Health,
    Entertainment,
    Tech,
    Politics,
    Food,
    Travel
}
