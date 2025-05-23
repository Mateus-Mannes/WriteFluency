using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WriteFluency.Propositions;

public class Proposition
{
    public int Id { get; set; }

    public required DateTime PublishedOn { get; set; }
    public required SubjectEnum SubjectId { get; set; }
    public required ComplexityEnum ComplexityId { get; set; }

    public required Guid AudioFileId { get; set; }
    [MaxLength(50)]
    public required string Voice { get; set; }

    [MaxLength(3000)]
    public required string Text { get; set; }
    public required int TextLength { get; set; }

    public Guid? ImageFileId { get; set; }

    public required DateTime CreatedAt { get; set; }

    public required NewsInfo NewsInfo { get; set; }

    public Complexity? Complexity { get; set; }
    public Subject? Subject { get; set; }

    public readonly static List<(SubjectEnum, ComplexityEnum)> Parameters =
        Enum.GetValues<SubjectEnum>().SelectMany(subject => Enum.GetValues<ComplexityEnum>().Select(complexity => (subject, complexity)))
            .OrderBy(c => (int)c.subject)
            .ThenBy(c => (int)c.complexity)
            .ToList();
            
            
}
