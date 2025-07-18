using System.ComponentModel.DataAnnotations.Schema;

namespace WriteFluency.Domain.App;

public class AppSettings
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }

    [NotMapped]
    public readonly static (string Key, string Value) IsNewsWorkerActive = ("IsNewsWorkerActive", "false");
    [NotMapped]
    public readonly static (string Key, string Value) NewsWorkerCron = ("NewsWorkerCron", "0 12 * * *");
}
