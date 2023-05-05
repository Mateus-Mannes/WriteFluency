using System.ComponentModel;

namespace WriteFluencyApi.Shared.ListenAndWrite;

public enum ComplexityEnum
{
    [Description("Be simple and beginner friendly, using easy vocabulary and simple sentence structures.")]
    Beginner,
    [Description("Use moderate vocabulary and some complex sentences.")]
    Intermediate,
    [Description("Use sophisticated vocabulary and advanced sentence structures.")]
    Advanced
}