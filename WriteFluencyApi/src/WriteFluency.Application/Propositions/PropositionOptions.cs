namespace WriteFluency.Propositions;

public class PropositionOptions
{
    public const string Section = "Propositions";
    public bool IsWorkerActive { get; set; }
    public required int DailyRequestsLimit { get; set; }
    public required int PropositionsLimitPerTopic { get; set; }
    public required int NewsRequestLimit { get; set; }
    public required string DailyRunCron { get; set; }
}
