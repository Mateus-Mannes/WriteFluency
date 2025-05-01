using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WriteFluency.Propositions;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SubjectEnum
{
    [Description("Curiosities, something interesting that may be new for most people")]
    Curiosities,
    [Description("Technology, things related to innovation")]
    Technology,
    [Description("Science, something in the word of science (Physics, Chemistry, Biology, Earth Science, Environmental Science)")]
    Science,
    [Description("Travel, things that may be interesting for someone who love traveling")]
    Travel,
    [Description("Culture, facts about different cultures around the word")]
    Culture,
    [Description("Business, corporative and entrepreneur stuff")]
    Business,
    [Description("Finance, facts related to money, investing, saving, etc")]
    Finance,
    [Description("Movies, about very famous movies")]
    Movies,
    [Description("Books, about very famous books and their autors")]
    Books,
    [Description("Programming, something into the programming word, add some jokes here")]
    Programming
}
