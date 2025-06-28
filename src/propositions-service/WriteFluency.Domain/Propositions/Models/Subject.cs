using System.ComponentModel.DataAnnotations;

namespace WriteFluency.Propositions;

public class Subject
{
    public SubjectEnum Id { get; set; }
    [MaxLength(50)]
    public required string Description { get; set; }
}
