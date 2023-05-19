public record TextComparisionDto
{
    public (int, int) CorrectTextArea { get; set; }
    public (int, int) UserTextHilightedArea { get; set; }

    public TextComparisionDto((int, int) correctTextArea,  (int, int) userTextHilightedArea)
    {
        CorrectTextArea = correctTextArea;
        UserTextHilightedArea = userTextHilightedArea;
    }
}