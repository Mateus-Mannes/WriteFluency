using System.ComponentModel.DataAnnotations;

namespace WriteFluency.Propositions;

public record NewsInfo
{
    [MaxLength(100)]
    public required string Id { get; init; }
    [MaxLength(500)]
    public required string Title { get; init; }
    [MaxLength(500)]
    public required string Description { get; init; }
    [MaxLength(500)]
    public required string Url { get; init; }
    [MaxLength(500)]
    public required string ImageUrl { get; init; }
    [MaxLength(3000)]
    public required string Text { get; init; }
    public required int TextLength { get; init; }
}
