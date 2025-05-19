namespace WriteFluency.Propositions;

public class PropositionOptions
{
    public const string Section = "Propositions";
    public required int DailyRequestLimit { get; set; }
    public required int PropositionsLimitPerTopic { get; set; }
    public required int NewsRequestLimit { get; set; }
}
