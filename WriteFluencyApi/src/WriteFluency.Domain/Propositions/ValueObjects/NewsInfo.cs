using System;

namespace WriteFluency.Propositions;

public class NewsInfo
{
    public required string Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Url { get; set; }
    public required string ImageUrl { get; set; }
    public required string Text { get; set; }
    public int TextLength { get; set; }
}
