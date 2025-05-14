namespace WriteFluency.Propositions;

public class Proposition
{
    public int Id { get; set; }

    public required DateTime PublishedOn { get; set; }
    public required SubjectEnum SubjectId { get; set; }
    public required ComplexityEnum ComplexityId { get; set; }

    public required Guid AudioFileId { get; set; }
    public required string Voice { get; set; }
   
    public required string Text { get; set; }
    public int TextLength { get; set; }

    public Guid ImageFileId { get; set; }

    public DateTime CreatedAt { get; set; }

    public required NewsInfo NewsInfo { get; set; }
}
