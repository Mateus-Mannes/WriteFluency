using System.ComponentModel.DataAnnotations;

namespace WriteFluency.Propositions;

public class Complexity
{
    public ComplexityEnum Id { get; set; }
    [MaxLength(50)]
    public required string Description { get; set; }
}
